#!/bin/bash
set -e

# The input STL file should be mounted at /app/input/wing.stl
# The output JSON files will be written to /app/output

INPUT_STL="/app/input/wing.stl"
OUT_DIR="/app/output"
BOUNDARY_POINTS="${OUT_DIR}/boundary_points.json"
PRESSURE_FIELD="${OUT_DIR}/pressure_field.json"
WEIGHTS="/app/pinn/output/pinn_weights.pt"

echo "=================================================="
echo "    F1 Capstone PINN Inference Pipeline           "
echo "=================================================="

if [ ! -f "$INPUT_STL" ]; then
    echo "ERROR: Input STL not found at $INPUT_STL"
    echo "Make sure to mount it: -v /path/to/stl:/app/input/wing.stl"
    exit 1
fi

mkdir -p "$OUT_DIR"

echo ""
echo "=== Step 1: C++ Boundary Extractor ==="
/app/bin/boundary_extractor \
    --stl "$INPUT_STL" \
    --out "$BOUNDARY_POINTS" \
    --mode centroid

echo ""
echo "=== Step 2: PyTorch PINN Inference ==="
cd /app/pinn
python run_infer.py \
    --data "$BOUNDARY_POINTS" \
    --weights "$WEIGHTS" \
    --out "$PRESSURE_FIELD"

echo ""
echo "=== Pipeline Complete ==="
echo "Results written to $OUT_DIR"
