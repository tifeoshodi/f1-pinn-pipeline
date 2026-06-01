// json_writer.hpp
// Lightweight JSON writer — no external dependencies (no nlohmann/json).
// Writes boundary_points.json in the format expected by the PyTorch PINN.
//
// Output schema:
// {
//   "metadata": { "source_file", "n_triangles", "n_boundary_points",
//                 "samples_per_triangle", "wing_parameters", "bounds" },
//   "points":  [[x,y,z], ...],
//   "normals": [[nx,ny,nz], ...]
// }
// ─────────────────────────────────────────────────────────────────────────────
#pragma once
#include "surface_sampler.hpp"
#include <fstream>
#include <iomanip>
#include <sstream>
#include <stdexcept>
#include <string>
#include <filesystem>

namespace writer {

// Format a single float to 6 decimal places
static inline std::string fmt(float v) {
    std::ostringstream ss;
    ss << std::fixed << std::setprecision(6) << v;
    return ss.str();
}

// ── Wing parameter block (mirrors NX expression table) ───────────────────────
struct WingParams {
    double c1   = 300.0, c2  = 180.0, c3  = 120.0;  // chord [mm]
    double a1   = 3.0,   a2  = 18.0,  a3  = 28.0;   // AoA   [deg]
    double g12  = 15.0,  g23 = 12.0;                 // gap   [mm]
    double ov12 = 10.0, ov23 = 8.0;                  // overlap[mm]
    double b    = 950.0, RH  = 60.0;                 // span, ride height [mm]
};

// ── Write boundary_points.json ────────────────────────────────────────────────
void write(const std::string&        out_path,
           const sampler::SampleSet& samples,
           size_t                    n_triangles,
           int                       samples_per_tri,
           const std::string&        source_file,
           const WingParams&         wp = WingParams{})
{
    // Create output directory if needed
    std::filesystem::path p(out_path);
    if (p.has_parent_path())
        std::filesystem::create_directories(p.parent_path());

    std::ofstream f(out_path);
    if (!f.is_open())
        throw std::runtime_error("Cannot write output file: " + out_path);

    const auto& pts = samples.points;
    const auto& nrm = samples.normals;
    const size_t N  = pts.size();
    const auto   bb = sampler::bounding_box(pts);

    f << std::fixed << std::setprecision(6);
    f << "{\n";

    // ── metadata ─────────────────────────────────────────────────────────────
    f << "  \"metadata\": {\n";
    f << "    \"source_file\": \"" << source_file << "\",\n";
    f << "    \"n_triangles\": "         << n_triangles        << ",\n";
    f << "    \"n_boundary_points\": "   << N                  << ",\n";
    f << "    \"samples_per_triangle\": " << samples_per_tri   << ",\n";
    f << "    \"wing_parameters\": {\n";
    f << "      \"c1_mm\": "   << wp.c1   << ", \"c2_mm\": "  << wp.c2  << ", \"c3_mm\": "  << wp.c3  << ",\n";
    f << "      \"alpha1_deg\":"<< wp.a1   << ", \"alpha2_deg\":"<< wp.a2 << ", \"alpha3_deg\":"<< wp.a3 <<",\n";
    f << "      \"g12_mm\": "  << wp.g12  << ", \"g23_mm\": " << wp.g23 << ",\n";
    f << "      \"ov12_mm\": " << wp.ov12 << ", \"ov23_mm\": "<< wp.ov23<< ",\n";
    f << "      \"span_mm\": " << wp.b    << ", \"ride_height_mm\": " << wp.RH << "\n";
    f << "    },\n";
    f << "    \"bounds\": {\n";
    f << "      \"x_min\": " << bb.x_min << ", \"x_max\": " << bb.x_max << ",\n";
    f << "      \"y_min\": " << bb.y_min << ", \"y_max\": " << bb.y_max << ",\n";
    f << "      \"z_min\": " << bb.z_min << ", \"z_max\": " << bb.z_max << "\n";
    f << "    }\n";
    f << "  },\n";

    // ── points ────────────────────────────────────────────────────────────────
    f << "  \"points\": [\n";
    for (size_t i = 0; i < N; ++i) {
        f << "    [" << fmt(pts[i].x) << ", "
                     << fmt(pts[i].y) << ", "
                     << fmt(pts[i].z) << "]";
        f << (i + 1 < N ? ",\n" : "\n");
    }
    f << "  ],\n";

    // ── normals ───────────────────────────────────────────────────────────────
    f << "  \"normals\": [\n";
    for (size_t i = 0; i < N; ++i) {
        f << "    [" << fmt(nrm[i].x) << ", "
                     << fmt(nrm[i].y) << ", "
                     << fmt(nrm[i].z) << "]";
        f << (i + 1 < N ? ",\n" : "\n");
    }
    f << "  ]\n";

    f << "}\n";

    if (!f.good())
        throw std::runtime_error("I/O error while writing: " + out_path);
}

} // namespace writer
