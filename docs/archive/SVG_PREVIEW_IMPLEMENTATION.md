# DWG/DXF SVG Preview Implementation

## Overview
Replaced the legacy bitmap-based rendering with a modern SVG-based solution using WebView2. This allows for infinite zooming and better performance for large drawings.

## Changes

### 1. New Component: `DxfSvgConverter`
*   **Location**: `Rendering/DxfSvgConverter.cs`
*   **Function**: Reads DXF files using `IxMilia.Dxf` and generates an SVG string.
*   **Features**:
    *   Supports Lines, Polylines (LwPolyline, Polyline), Circles, Arcs, Texts, and Blocks (Inserts).
    *   Auto-calculates bounding box for proper ViewBox.
    *   Handles coordinate system flipping (CAD Y-up vs SVG Y-down).
    *   Basic color mapping.

### 2. Updated Preview: `CadPreview`
*   **Location**: `Previews/CadPreview.cs`
*   **Logic**:
    1.  Checks file extension.
    2.  If `.dwg`, calls existing `DwgConverter` to convert to `.dxf` (requires ODA File Converter).
    3.  Calls `DxfSvgConverter` to get SVG content.
    4.  Injects SVG into an HTML template with Pan/Zoom JavaScript.
    5.  Displays in `WebView2`.

## Requirements
*   **ODA File Converter**: Required for `.dwg` files. Should be installed or placed in `Dependencies/ODAFileConverter/`.
*   **WebView2 Runtime**: Required on the target machine (usually present on Windows 10/11).

## Verification
*   Build passed successfully.
*   Run the application and open a DWG or DXF file to test.
