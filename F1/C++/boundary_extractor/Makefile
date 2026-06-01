# ─────────────────────────────────────────────────────────────────────────────
# Makefile — F1 Front Wing Boundary Extractor
# Compiler: GCC (MSYS2/ucrt64 on Windows)
#
# IMPORTANT: Run from the MSYS2 UCRT64 terminal, NOT from PowerShell.
#   Start Menu → MSYS2 UCRT64 → navigate here → make
#
# OR from PowerShell (adds MSYS2 to PATH automatically):
#   $env:PATH = "C:\msys64\ucrt64\bin;C:\msys64\usr\bin;" + $env:PATH
#   make  (this calls the Makefile which uses the full path to g++)
#
# Targets:
#   make          — build the executable
#   make run      — build and run centroid mode (1,920 pts)
#   make run-bary — build and run barycentric mode (5 samples/tri = 9,600 pts)
#   make clean    — remove build artifacts
# ─────────────────────────────────────────────────────────────────────────────

GPP      := C:/msys64/ucrt64/bin/g++.exe
CXXFLAGS := -std=c++17 -O2 -Wall -Wextra
IFLAGS   := -Iinclude
TARGET   := boundary_extractor.exe
SRC      := src/main.cpp

STL_PATH := "../../STL Files/F1_FrontWing_Parametric_2.stl"
OUT_CENT := output/boundary_points.json
OUT_BARY := output/boundary_points_bary5.json

# ─────────────────────────────────────────────────────────────────────────────
all: $(TARGET)

$(TARGET): $(SRC)
	$(GPP) $(CXXFLAGS) $(IFLAGS) -o $@ $^
	@echo ""
	@echo "Build OK: $(TARGET)"
	@echo "Run:  make run"

run: $(TARGET)
	./$(TARGET) --stl $(STL_PATH) --out $(OUT_CENT) --mode centroid

run-bary: $(TARGET)
	./$(TARGET) --stl $(STL_PATH) --out $(OUT_BARY) --mode barycentric --samples 5

clean:
	rm -f $(TARGET) src/*.o

.PHONY: all run run-bary clean
