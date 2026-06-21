# Deploying .NET API + Angular UI to Azure Kubernetes Service (AKS)

## Overview

This document covers the end-to-end deployment of a .NET 10 Web API and Angular 18 UI to Azure Kubernetes Service (AKS) using Docker and Azure Container Registry (ACR).

**Stack:**
- Backend: .NET 10 Web API (ASP.NET Core)
- Frontend: Angular 18 (served via nginx)
- Database: PostgreSQL (deployed inside Kubernetes)
- Container Registry: Azure Container Registry (ACR)
- Orchestration: Azure Kubernetes Service (AKS)

---

## Architecture

```
Browser
  ↓ (public IP via LoadBalancer)
Angular UI Pod (nginx)
  ↓ (API calls)
.NET API Pod (LoadBalancer)
  ↓
PostgreSQL Pod (ClusterIP - internal only)
```

---

## Prerequisites

- Azure CLI (`az`)
- Docker Desktop
- kubectl
- Active Azure subscription

---

## Step 1 — Login to Azure

```bash
az login
az account show   # verify correct subscription
```

---

## Step 2 — Create Resource Group and Container Registry

A **Resource Group** is a logical container for all Azure resources in this project.  
**ACR** is where Docker images are stored.

```bash
# Create resource group
az group create --name comments-app-rg --location eastus

# Register ACR provider (first time only)
az provider register --namespace Microsoft.ContainerRegistry
az provider show --namespace Microsoft.ContainerRegistry --query registrationState

# Create container registry
az acr create -g comments-app-rg -n commentsappregistry --sku Basic

# Enable admin access
az acr update --name commentsappregistry --admin-enabled true
```

---

## Step 3 — Dockerize the .NET API

Create `Dockerfile` in the API project root:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY my-comment-api.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "my-comment-api.dll"]
```

Key points:
- **Multi-stage build** — SDK image for building, smaller ASP.NET runtime image for running
- Reduces final image size significantly

---

## Step 4 — Dockerize the Angular UI

Create `Dockerfile` in the Angular project root:

```dockerfile
# Build stage
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json .
RUN npm install
COPY . .
RUN npm run build -- --configuration production

# Runtime stage
FROM nginx:alpine AS runtime
COPY --from=build /app/dist/comment-api/browser /usr/share/nginx/html
EXPOSE 80
```

Key points:
- `--configuration production` ensures Angular uses `environment.ts` (not `environment.development.ts`)
- nginx serves the static Angular files
- All service files must import from `environment` not `environment.development`

---

## Step 5 — Build and Push Images to ACR

**Important:** When building on Apple Silicon (ARM) for AKS (AMD64), always specify the platform:

```bash
# Login to ACR
az acr login --name commentsappregistry

# Build and push API image for linux/amd64
cd my-comment-api
docker buildx build --platform linux/amd64 \
  -t commentsappregistry.azurecr.io/comments-api:v1 --push .

# Build and push UI image for linux/amd64
cd ../comment-api
docker buildx build --platform linux/amd64 \
  -t commentsappregistry.azurecr.io/comments-ui:v1 --push .

# Verify images in ACR
az acr repository list --name commentsappregistry --output table
```

---

## Step 6 — Create AKS Cluster

```bash
az aks create \
  --resource-group comments-app-rg \
  --name comments-aks \
  --node-count 2 \
  --node-vm-size Standard_DC2s_v3 \
  --attach-acr commentsappregistry \
  --generate-ssh-keys
```

- `--node-count 2` — 2 VMs to run pods
- `--attach-acr` — grants AKS permission to pull from ACR automatically
- `--node-vm-size` — choose a size allowed by your subscription

Check status:
```bash
az aks show --resource-group comments-app-rg --name comments-aks --query provisioningState
```

---

## Step 7 — Connect kubectl to AKS (Access Cloud Cluster Locally)

Even though the cluster is running in Azure, you control it from your local terminal using `kubectl`. The bridge is the `~/.kube/config` file — it stores credentials and cluster addresses for every cluster you've connected to.

```bash
az aks get-credentials --resource-group comments-app-rg --name comments-aks
```

**What this command does step by step:**
1. Authenticates to Azure and finds your AKS cluster
2. Downloads the cluster's API server address and certificates
3. Merges them into `~/.kube/config` on your local machine
4. Sets the AKS cluster as the **current context**

From this point, every `kubectl` command you run locally is sent securely over HTTPS to the AKS control plane in Azure.

```bash
# Verify connection
kubectl get nodes   # should show 2 nodes in Ready state

# See what cluster kubectl is currently pointing to
kubectl config current-context

# See all clusters in your kubeconfig
kubectl config get-contexts

# Switch between clusters (e.g. local vs cloud)
kubectl config use-context <context-name>
```

**How it works internally:**
```
Your Terminal
  ↓ kubectl command
~/.kube/config  ← contains AKS cluster address + credentials
  ↓ HTTPS
AKS Control Plane (Azure-managed)
  ↓
Your Nodes (VMs running pods)
```

This is why you never need to SSH into the cluster nodes directly — `kubectl` handles everything through the control plane API.

---

## Step 8 — Kubernetes Manifests

### PostgreSQL (`postgres-deployment.yaml`)

```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: postgres-pvc
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 1Gi
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: postgres
spec:
  replicas: 1
  selector:
    matchLabels:
      app: postgres
  template:
    metadata:
      labels:
        app: postgres
    spec:
      containers:
        - name: postgres
          image: postgres:16-alpine
          ports:
            - containerPort: 5432
          env:
            - name: POSTGRES_DB
              value: "commentsdb"
            - name: POSTGRES_USER
              value: "postgres"
            - name: POSTGRES_PASSWORD
              value: "postgres"
          volumeMounts:
            - name: postgres-storage
              mountPath: /var/lib/postgresql/data
              subPath: pgdata        # avoids lost+found conflict
      volumes:
        - name: postgres-storage
          persistentVolumeClaim:
            claimName: postgres-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: postgres-service
spec:
  selector:
    app: postgres
  ports:
    - port: 5432
      targetPort: 5432
  type: ClusterIP
```

Key points:
- `PersistentVolumeClaim` — requests disk storage so data survives pod restarts
- `subPath: pgdata` — avoids the `lost+found` directory conflict on Azure disks
- `ClusterIP` — Postgres is only accessible inside the cluster
- Service named `postgres-service` — API connects using this hostname

### API (`api-deployment.yaml`)

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: comments-api
spec:
  replicas: 2
  selector:
    matchLabels:
      app: comments-api
  template:
    metadata:
      labels:
        app: comments-api
    spec:
      containers:
        - name: comments-api
          image: commentsappregistry.azurecr.io/comments-api:v1
          ports:
            - containerPort: 8080
          env:
            - name: ConnectionStrings__DefaultConnection
              value: "Host=postgres-service;Port=5432;Database=commentsdb;Username=postgres;Password=postgres"
            - name: JwtSettings__SecretKey
              value: "your-super-secret-key-that-is-at-least-32-characters"
            - name: JwtSettings__Issuer
              value: "CommentsApi"
            - name: JwtSettings__Audience
              value: "CommentsApiUsers"
            - name: JwtSettings__ExpiryInMinutes
              value: "60"
---
apiVersion: v1
kind: Service
metadata:
  name: comments-api-service
spec:
  selector:
    app: comments-api
  ports:
    - port: 80
      targetPort: 8080
  type: LoadBalancer
```

Key points:
- `replicas: 2` — 2 instances for availability
- Environment variables override `appsettings.json` at runtime
- `Host=postgres-service` — Kubernetes DNS resolves this to the Postgres pod
- `LoadBalancer` — Azure assigns a public IP for the API
- In production use Kubernetes Secrets instead of plain env vars

### UI (`ui-deployment.yaml`)

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: comments-ui
spec:
  replicas: 1
  selector:
    matchLabels:
      app: comments-ui
  template:
    metadata:
      labels:
        app: comments-ui
    spec:
      containers:
        - name: comments-ui
          image: commentsappregistry.azurecr.io/comments-ui:v1
          ports:
            - containerPort: 80
---
apiVersion: v1
kind: Service
metadata:
  name: comments-ui-service
spec:
  selector:
    app: comments-ui
  ports:
    - port: 80
      targetPort: 80
  type: LoadBalancer
```

---

## Step 9 — Auto-run Migrations on Startup

Add to `ConfigureApp.cs` so the database schema is created automatically on first run:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
```

---

## Step 10 — Deploy to AKS

```bash
kubectl apply -f k8s/postgres-deployment.yaml
kubectl apply -f k8s/api-deployment.yaml
kubectl apply -f k8s/ui-deployment.yaml
```

Monitor pods:
```bash
kubectl get pods -w
kubectl get services
```

Get public IPs:
```bash
kubectl get services
# EXTERNAL-IP of comments-api-service  → API public IP
# EXTERNAL-IP of comments-ui-service   → UI public IP
```

---

## Common Issues and Fixes

| Issue | Cause | Fix |
|---|---|---|
| `exec format error` | Image built for ARM, AKS runs AMD64 | Use `--platform linux/amd64` in docker buildx |
| `lost+found` Postgres error | Azure disk mount conflict | Add `subPath: pgdata` to volumeMount |
| `Multi-Attach error` for PVC | Old pod still holds the PVC | Delete old deployment and PVC, redeploy |
| Angular calling `localhost` | Service imports `environment.development` | Change import to `environment` |
| CORS errors | API only allows `localhost:4200` | Add UI public IP to CORS allowed origins |
| 404 on all API routes | DB migrations not run | Add `db.Database.Migrate()` on startup |
| `ErrImagePull` | Not logged into ACR | Run `az acr login --name commentsappregistry` |

---

## Useful kubectl Commands

```bash
# View all pods
kubectl get pods

# View logs
kubectl logs -l app=comments-api --tail=50

# Describe a pod (events, errors)
kubectl describe pod <pod-name>

# Restart a deployment
kubectl rollout restart deployment/comments-api

# Check rollout status
kubectl rollout status deployment/comments-api

# Execute command inside a pod
kubectl exec -it <pod-name> -- /bin/sh

# Delete and recreate
kubectl delete deployment postgres
kubectl delete pvc postgres-pvc
kubectl apply -f k8s/postgres-deployment.yaml
```

---

## Key Concepts for Interview

| Concept | What it is |
|---|---|
| **Resource Group** | Logical container for all Azure resources — easy billing, access control, and cleanup |
| **ACR** | Private Docker registry on Azure — stores your images securely |
| **AKS** | Managed Kubernetes cluster — Azure handles the control plane |
| **Pod** | Smallest deployable unit in Kubernetes — runs one or more containers |
| **Deployment** | Manages pods — handles scaling, rolling updates, restarts |
| **Service** | Exposes pods — `ClusterIP` (internal), `LoadBalancer` (public IP) |
| **PersistentVolumeClaim** | Requests storage that survives pod restarts |
| **ConfigMap / Secret** | Store configuration and sensitive data separately from the image |
| **Kubernetes DNS** | Every Service gets a DNS name — pods find each other by service name |
| **Multi-stage Docker build** | Separate build and runtime stages — smaller, more secure final image |
| **Rolling update** | Kubernetes replaces pods one by one — zero downtime deployments |
