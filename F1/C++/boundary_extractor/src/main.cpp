// main.cpp — F1 Front Wing Boundary Extractor
// ─────────────────────────────────────────────────────────────────────────────
// Reads a binary STL (NX Student Edition export), samples surface boundary
// points and outward normals, and writes boundary_points.json for the PINN.
//
// Usage:
//   boundary_extractor [options]
//
//   --stl    <path>   Path to input .stl file
//                     (default: ../../STL Files/F1_FrontWing_Parametric_2.stl)
//   --out    <path>   Path for output .json file
//                     (default: output/boundary_points.json)
//   --mode   centroid | barycentric
//                     Sampling mode (default: centroid)
//   --samples <n>     Points per triangle in barycentric mode (default: 3)
//   --seed   <n>      RNG seed for barycentric mode (default: 42)
//   --help            Print this message
//
// Examples:
//   boundary_extractor
//   boundary_extractor --mode barycentric --samples 5
//   boundary_extractor --stl my_wing.stl --out results/pts.json
// ─────────────────────────────────────────────────────────────────────────────
#include "stl_reader.hpp"
#include "surface_sampler.hpp"
#include "json_writer.hpp"

#include <chrono>
#include <filesystem>
#include <iostream>
#include <string>

// ── Minimal CLI argument parser ───────────────────────────────────────────────
struct Args {
    std::string stl_path  = "../../STL Files/F1_FrontWing_Parametric_2.stl";
    std::string out_path  = "output/boundary_points.json";
    std::string mode      = "centroid";   // "centroid" or "barycentric"
    int         samples   = 3;            // per-triangle count for barycentric
    uint32_t    seed      = 42;
    bool        help      = false;
};

Args parse_args(int argc, char* argv[]) {
    Args a;
    for (int i = 1; i < argc; ++i) {
        std::string s(argv[i]);
        if (s == "--help" || s == "-h") { a.help = true; }
        else if (s == "--stl"     && i+1 < argc) { a.stl_path = argv[++i]; }
        else if (s == "--out"     && i+1 < argc) { a.out_path = argv[++i]; }
        else if (s == "--mode"    && i+1 < argc) { a.mode     = argv[++i]; }
        else if (s == "--samples" && i+1 < argc) { a.samples  = std::stoi(argv[++i]); }
        else if (s == "--seed"    && i+1 < argc) { a.seed     = static_cast<uint32_t>(std::stoul(argv[++i])); }
        else {
            std::cerr << "Unknown argument: " << s << "  (use --help)\n";
        }
    }
    return a;
}

void print_help(const char* prog) {
    std::cout <<
        "Usage: " << prog << " [options]\n\n"
        "  --stl    <path>   Input binary STL  (default: ../../STL Files/F1_FrontWing_Parametric_2.stl)\n"
        "  --out    <path>   Output JSON        (default: output/boundary_points.json)\n"
        "  --mode   <str>    centroid | barycentric  (default: centroid)\n"
        "  --samples <n>     Points per tri for barycentric mode (default: 3)\n"
        "  --seed   <n>      RNG seed for barycentric mode (default: 42)\n"
        "  --help            Print this message\n";
}

// ── Separator utility ─────────────────────────────────────────────────────────
static void sep() { std::cout << std::string(56, '-') << '\n'; }

// ── Main ──────────────────────────────────────────────────────────────────────
int main(int argc, char* argv[]) {

    const Args a = parse_args(argc, argv);
    if (a.help) { print_help(argv[0]); return 0; }

    auto t0 = std::chrono::steady_clock::now();

    std::cout << "\nF1 Front Wing — Boundary Extractor\n";
    sep();

    // ── 1. Read STL ───────────────────────────────────────────────────────────
    std::cout << "Input  : " << a.stl_path << '\n';
    std::vector<stl::Triangle> tris;
    try {
        tris = stl::read(a.stl_path);
    } catch (const std::exception& e) {
        std::cerr << "ERROR reading STL: " << e.what() << '\n';
        return 1;
    }
    std::cout << "Triangles loaded : " << tris.size() << '\n';

    // ── 2. Sample surface ─────────────────────────────────────────────────────
    sampler::SampleSet samples;
    int spt = 1;  // samples per triangle (for metadata)

    if (a.mode == "centroid") {
        std::cout << "Mode   : centroid  (1 point per triangle)\n";
        samples = sampler::centroid_sample(tris);
        spt = 1;
    } else if (a.mode == "barycentric") {
        std::cout << "Mode   : barycentric  (" << a.samples
                  << " points per triangle, seed=" << a.seed << ")\n";
        samples = sampler::barycentric_sample(tris, a.samples, a.seed);
        spt = a.samples;
    } else {
        std::cerr << "ERROR: Unknown mode '" << a.mode
                  << "'. Use 'centroid' or 'barycentric'.\n";
        return 1;
    }

    std::cout << "Boundary points  : " << samples.points.size() << '\n';

    // ── 3. Bounding box diagnostics ───────────────────────────────────────────
    const auto bb = sampler::bounding_box(samples.points);
    sep();
    std::cout << "Bounding box (mm):\n"
              << "  X  [" << bb.x_min << ", " << bb.x_max
              << "]  span = " << (bb.x_max - bb.x_min) << " mm\n"
              << "  Y  [" << bb.y_min << ", " << bb.y_max
              << "]  span = " << (bb.y_max - bb.y_min) << " mm\n"
              << "  Z  [" << bb.z_min << ", " << bb.z_max
              << "]  span = " << (bb.z_max - bb.z_min) << " mm\n";

    // Sanity check: X span should be roughly (c1+c2+c3)*2 ≈ 1200mm (full span)
    // Y span should be ≈ 2*b = 1900mm (mirrored)
    // Z should include ride height + wing thickness
    const float x_span = bb.x_max - bb.x_min;
    const float y_span = bb.y_max - bb.y_min;
    if (x_span < 100.0f || x_span > 3000.0f)
        std::cerr << "  WARNING: X span (" << x_span
                  << " mm) looks wrong — expected ~600–1200 mm for this wing.\n";
    if (y_span < 100.0f || y_span > 5000.0f)
        std::cerr << "  WARNING: Y span (" << y_span
                  << " mm) looks wrong — expected ~950–1900 mm for this wing.\n";

    // ── 4. Write JSON ─────────────────────────────────────────────────────────
    sep();
    std::cout << "Output : " << a.out_path << '\n';

    writer::WingParams wp;   // defaults match NX expression values
    try {
        writer::write(a.out_path, samples, tris.size(), spt,
                      std::filesystem::path(a.stl_path).filename().string(), wp);
    } catch (const std::exception& e) {
        std::cerr << "ERROR writing JSON: " << e.what() << '\n';
        return 1;
    }

    const auto dt = std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::steady_clock::now() - t0).count();
    sep();
    std::cout << "Done in " << dt << " ms\n\n";
    return 0;
}
