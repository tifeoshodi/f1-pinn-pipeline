# Stage 1: Build the C++ Boundary Extractor
FROM ubuntu:22.04 AS cpp-builder
RUN apt-get update && apt-get install -y g++ make

WORKDIR /build
# Copy the C++ source code
COPY C++/boundary_extractor/include ./include
COPY C++/boundary_extractor/src ./src

# Compile directly (bypassing the MSYS2 Makefile which has Windows paths)
RUN g++ -std=c++17 -O2 -Wall -Wextra -Iinclude src/main.cpp -o boundary_extractor

# Stage 2: Python Runtime and PINN Inference
FROM python:3.11-slim
WORKDIR /app

# Install PyTorch CPU (slimmer than standard PyTorch)
RUN pip install --no-cache-dir torch --index-url https://download.pytorch.org/whl/cpu

# Copy the PINN python code and pre-trained weights
COPY pinn /app/pinn

# Copy the compiled C++ executable from Stage 1
COPY --from=cpp-builder /build/boundary_extractor /app/bin/boundary_extractor

# Create the pipeline shell script
COPY run_pipeline.sh /app/run_pipeline.sh
RUN chmod +x /app/run_pipeline.sh

# Default command runs the full pipeline
ENTRYPOINT ["/app/run_pipeline.sh"]
