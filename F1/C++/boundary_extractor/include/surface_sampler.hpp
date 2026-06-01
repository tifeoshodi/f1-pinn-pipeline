// surface_sampler.hpp
// Samples boundary points and normals from a triangle mesh.
//
// Two modes:
//   CENTROID  (samples_per_tri = 1)  — one point at the centroid of each triangle.
//                                      Fast, 1,920 points for the F1 wing STL.
//   BARYCENTRIC (samples_per_tri > 1) — N uniformly-random points per triangle
//                                       for denser coverage on larger triangles.
//
// Output normals are computed from the cross-product of triangle edges so they
// are always consistent, regardless of the NX-exported normal field quality.
// ─────────────────────────────────────────────────────────────────────────────
#pragma once
#include "stl_reader.hpp"
#include <cmath>
#include <random>
#include <stdexcept>
#include <vector>

namespace sampler {

// ── 3D vector helpers ─────────────────────────────────────────────────────────
using V3 = stl::Vec3f;

inline V3 sub(const V3& a, const V3& b) {
    return {a.x - b.x, a.y - b.y, a.z - b.z};
}
inline V3 add(const V3& a, const V3& b) {
    return {a.x + b.x, a.y + b.y, a.z + b.z};
}
inline V3 scale(const V3& a, float s) {
    return {a.x * s, a.y * s, a.z * s};
}
inline V3 cross(const V3& a, const V3& b) {
    return {
        a.y * b.z - a.z * b.y,
        a.z * b.x - a.x * b.z,
        a.x * b.y - a.y * b.x
    };
}
inline float norm(const V3& v) {
    return std::sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
}
inline V3 normalise(const V3& v) {
    float n = norm(v);
    if (n < 1e-12f) return {0.0f, 0.0f, 1.0f};  // degenerate triangle guard
    return {v.x / n, v.y / n, v.z / n};
}

// Compute outward unit normal from edge cross-product (CCW vertex order assumed)
inline V3 face_normal(const stl::Triangle& t) {
    return normalise(cross(sub(t.v[1], t.v[0]), sub(t.v[2], t.v[0])));
}

// Triangle centroid
inline V3 centroid(const stl::Triangle& t) {
    return scale(add(add(t.v[0], t.v[1]), t.v[2]), 1.0f / 3.0f);
}

// ── Result type ───────────────────────────────────────────────────────────────
struct SampleSet {
    std::vector<V3> points;   // surface positions [mm]
    std::vector<V3> normals;  // outward unit normals
};

// ── Centroid sampling (1 point per triangle) ──────────────────────────────────
SampleSet centroid_sample(const std::vector<stl::Triangle>& tris) {
    SampleSet out;
    out.points.reserve(tris.size());
    out.normals.reserve(tris.size());
    for (const auto& t : tris) {
        out.points.push_back(centroid(t));
        out.normals.push_back(face_normal(t));
    }
    return out;
}

// ── Uniform barycentric sampling (N points per triangle) ─────────────────────
// Uses the Osada et al. folding trick for uniform area sampling.
SampleSet barycentric_sample(const std::vector<stl::Triangle>& tris,
                              int samples_per_tri,
                              uint32_t seed = 42) {
    if (samples_per_tri < 1)
        throw std::invalid_argument("samples_per_tri must be >= 1");

    std::mt19937 rng(seed);
    std::uniform_real_distribution<float> uni(0.0f, 1.0f);

    SampleSet out;
    out.points.reserve(tris.size() * static_cast<size_t>(samples_per_tri));
    out.normals.reserve(tris.size() * static_cast<size_t>(samples_per_tri));

    for (const auto& t : tris) {
        const V3  n   = face_normal(t);
        const V3& v0  = t.v[0];
        const V3  e1  = sub(t.v[1], v0);  // edge v0→v1
        const V3  e2  = sub(t.v[2], v0);  // edge v0→v2

        for (int s = 0; s < samples_per_tri; ++s) {
            float r1 = uni(rng);
            float r2 = uni(rng);
            // Fold into triangle so point stays inside
            if (r1 + r2 > 1.0f) { r1 = 1.0f - r1; r2 = 1.0f - r2; }
            // p = v0 + r1*(v1-v0) + r2*(v2-v0)
            V3 p = add(v0, add(scale(e1, r1), scale(e2, r2)));
            out.points.push_back(p);
            out.normals.push_back(n);
        }
    }
    return out;
}

// ── Bounding box diagnostics ──────────────────────────────────────────────────
struct BBox {
    float x_min, x_max;
    float y_min, y_max;
    float z_min, z_max;
};

BBox bounding_box(const std::vector<V3>& pts) {
    if (pts.empty()) return {0,0,0,0,0,0};
    BBox bb = {pts[0].x, pts[0].x, pts[0].y, pts[0].y, pts[0].z, pts[0].z};
    for (const auto& p : pts) {
        bb.x_min = std::min(bb.x_min, p.x); bb.x_max = std::max(bb.x_max, p.x);
        bb.y_min = std::min(bb.y_min, p.y); bb.y_max = std::max(bb.y_max, p.y);
        bb.z_min = std::min(bb.z_min, p.z); bb.z_max = std::max(bb.z_max, p.z);
    }
    return bb;
}

} // namespace sampler
