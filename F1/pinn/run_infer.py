import argparse
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

import torch
from model import PINN
from data import load
from infer import run_inference

def main():
    p = argparse.ArgumentParser(description="Run PINN Inference only")
    p.add_argument("--data", required=True, help="Path to boundary_points.json")
    p.add_argument("--weights", required=True, help="Path to trained pinn_weights.pt")
    p.add_argument("--out", required=True, help="Path to output pressure_field.json")
    args = p.parse_args()

    print(f"\n[run_infer] Loading boundary data from: {args.data}")
    # We don't need many interior points for inference (only used to define coordinates)
    ds = load(args.data, n_interior=100)

    print(f"[run_infer] Loading model weights from: {args.weights}")
    model = PINN()
    model.load_state_dict(torch.load(args.weights, map_location="cpu"))
    model.eval()

    run_inference(model, ds, out_path=args.out)

if __name__ == "__main__":
    main()
