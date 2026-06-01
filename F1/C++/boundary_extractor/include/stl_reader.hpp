// stl_reader.hpp
// Binary STL parser — reads NX-exported .stl into a flat Triangle array.
// Spec: 80-byte header | uint32 n_triangles | n × (12+12+12+12+2 bytes)
// ─────────────────────────────────────────────────────────────────────────
#pragma once
#include <cstdint>
#include <fstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace stl {

struct Vec3f {
    float x, y, z;
};

struct Triangle {
    Vec3f normal;   // face normal as exported by NX (may not be unit length)
    Vec3f v[3];     // vertices in CCW order (right-hand rule → outward normal)
};

// Returns the parsed triangle list from a binary STL file.
// Throws std::runtime_error on I/O or format errors.
std::vector<Triangle> read(const std::string& path) {
    std::ifstream f(path, std::ios::binary | std::ios::ate);
    if (!f.is_open())
        throw std::runtime_error("Cannot open STL file: " + path);

    const std::streamsize file_size = f.tellg();
    if (file_size < 84)
        throw std::runtime_error("File too small to be a valid binary STL: " + path);

    f.seekg(0, std::ios::beg);

    // ── 80-byte header ────────────────────────────────────────────────────
    char header[80];
    f.read(header, 80);

    // ── Triangle count ────────────────────────────────────────────────────
    uint32_t n = 0;
    f.read(reinterpret_cast<char*>(&n), 4);

    // Validate file size: 84 + n * 50 bytes expected
    const std::streamsize expected = 84 + static_cast<std::streamsize>(n) * 50;
    if (file_size != expected) {
        // Might be ASCII STL or corrupted
        throw std::runtime_error(
            "File size mismatch — expected binary STL with " +
            std::to_string(n) + " triangles (" + std::to_string(expected) +
            " bytes) but got " + std::to_string(file_size) + " bytes.\n"
            "Ensure the file is exported as BINARY STL (not ASCII).");
    }

    // ── Read triangles ────────────────────────────────────────────────────
    std::vector<Triangle> tris(n);
    for (uint32_t i = 0; i < n; ++i) {
        f.read(reinterpret_cast<char*>(&tris[i].normal),  12);
        f.read(reinterpret_cast<char*>(&tris[i].v[0]),    12);
        f.read(reinterpret_cast<char*>(&tris[i].v[1]),    12);
        f.read(reinterpret_cast<char*>(&tris[i].v[2]),    12);
        uint16_t attr = 0;
        f.read(reinterpret_cast<char*>(&attr), 2);  // attribute bytes — discard
    }

    return tris;
}

} // namespace stl
