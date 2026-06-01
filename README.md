# F1 PINN Aerodynamics Pipeline

**Phase 1 Capstone Project**  
An end-to-end, containerized Physics-Informed Neural Network (PINN) pipeline for predicting aerodynamic pressure distributions on a multi-element F1 front wing.

## 🏎️ Overview
This repository contains a fully automated pipeline that:
1. Takes a 3D CAD model (`.stl`) of an F1 front wing.
2. Extracts and samples the surface boundary points using a custom **C++ Boundary Extractor**.
3. Feeds those coordinates into a **PyTorch PINN**, which uses pre-trained weights to enforce incompressible Navier-Stokes physics and predict the surface pressure coefficient ($C_p$).
4. Outputs the aerodynamic data to a structured JSON file.

## 🛠️ Architecture
- **Task 1 (CAD):** Siemens NX parameterized 3-element wing.
- **Task 2 (C++ Extractor):** Zero-dependency binary STL reader and surface sampler (runs in ~70ms).
- **Task 3 (PyTorch PINN):** Multi-layer perceptron trained on boundary conditions, inlet free-stream constraints, and physical PDE residuals.
- **Task 4 (Docker & CI/CD):** Multi-stage Docker container and GitHub Actions integration.

---

## 🚀 How to Run Locally

You don't need to install C++, Python, or PyTorch to run this pipeline. The only requirement is **Docker**.

### 1. Clone the repository
```bash
git clone https://github.com/tifeoshodi/f1-pinn-pipeline.git
cd f1-pinn-pipeline
```

### 2. Build the Docker Image
This spins up an Ubuntu environment, compiles the C++ extractor from source, and sets up the PyTorch CPU runtime.
```bash
docker build -t f1-pinn-pipeline -f F1/Dockerfile .
```

### 3. Run the Pipeline
Mount the provided STL file and an output directory into the container:
```bash
mkdir -p pipeline_output

docker run --rm \
  -v "$(pwd)/F1/STL Files/F1_FrontWing_Parametric_2.stl:/app/input/wing.stl" \
  -v "$(pwd)/pipeline_output:/app/output" \
  f1-pinn-pipeline
```

You will see the C++ tool run, followed immediately by PyTorch inference. The final predicted pressure field will be saved to `pipeline_output/pressure_field.json`.

---

## ☁️ Continuous Integration (CI/CD)
This repository is configured with **GitHub Actions**. 
Every time code is pushed to the `main` branch, GitHub automatically spins up a runner, builds the Docker container, executes the physics pipeline against the latest STL, and uploads the final `pressure_field.json` as a downloadable artifact. You can view these runs in the **Actions** tab!
