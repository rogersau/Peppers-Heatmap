# DayZ Heatmap API

This ASP.NET Core API generates heatmap images from DayZ server admin log (`.ADM`) files. It can produce both smooth heatmaps and grid-based pixel maps, with options for weighting different types of death events.

Inspired by the work done in [SumrakDZN/DeathMap](https://github.com/SumrakDZN/DeathMap).

## Features

*   Processes DayZ `.ADM` log files to extract death/kill event positions.
*   Generates heatmap images based on event density.
*   Generates pixel map images based on event counts within grid cells.
*   Supports multiple DayZ maps (ChernarusPlus, Livonia, Namalsk, Sakhal, Deadfall) with default background images.
*   Allows uploading custom background images.
*   Optional weighting for different death types (e.g., long-distance kills vs. zombie kills) to influence visual intensity.
*   Provides endpoints to return the raw image or image data along with metadata (total deaths, etc.).
*   Includes basic player trace map generation (separate endpoint).

## API Endpoints

### Death Maps

*   **`POST /api/DeathMap/generate`**
    *   **Description**: Generates a heatmap or pixel map image and returns it directly.
    *   **Request**: `multipart/form-data` containing:
        *   `AdminLogFiles`: One or more `.ADM` files.
        *   `Configuration` (optional, JSON string or form fields):
            *   `MapName`: (string, enum: `ChernarusPlus`, `Livonia`, `Namalsk`, `Sakhal`, `Deadfall`, default: `Namalsk`)
            *   `OutputSize`: (integer, optional, default: 1024 or map default)
            *   `OutputType`: (string, `heat` or `pixel`, default: `heat`)
            *   `UseWeighting`: (boolean, default: `false`)
            *   `BackgroundImage`: (file, optional)
    *   **Response**: `image/png`

*   **`POST /api/DeathMap/generateWithMetadata`**
    *   **Description**: Generates the map and returns a JSON response containing image data (Base64 encoded) and metadata.
    *   **Request**: Same as `/generate`.
    *   **Response**: `application/json` (`HeatmapResponse` object)
        ```json
        {
          "imageFormat": "png",
          "imageData": "data:image/png;base64,...",
          "totalDeaths": 150, // Accurate count of death events
          "outOfBoundsDeaths": 5, // Count of positions processed for map that were OOB
          "success": true,
          "errorMessage": null
        }
        ```

### Player Traces (Experimental)

*   **`POST /api/PlayerTrace/generate`**
    *   **Description**: Generates a player trace map image.
    *   **Request**: `multipart/form-data` (similar structure to DeathMap, uses `PlayerTraceConfiguration`)
    *   **Response**: `image/png`

*   **`POST /api/PlayerTrace/generateWithMetadata`**
    *   **Description**: Generates a player trace map and returns JSON with image data and metadata.
    *   **Request**: Same as `/generate`.
    *   **Response**: `application/json` (`PlayerTraceResponse` object)

## Configuration Options (`DeathMapConfiguration`)

*   **`MapName`**: Specifies the base map. Determines default background image and coordinate scaling (`MapSize`).
*   **`OutputSize`**: The width and height (in pixels) of the generated square image. Defaults vary by map if not set.
*   **`OutputType`**: `heat` for smooth Gaussian-based heatmap, `pixel` for grid-based map.
*   **`UseWeighting`**: If `true`, different death events contribute differently to the visual intensity based on predefined weights (see Heatmap Generation section). If `false`, all events contribute equally.
*   **`BackgroundImage`**: An optional image file to use instead of the default map background.

## Heatmap Generation Process

This section outlines how the DayZ Heatmap API generates heatmap images from admin log files, detailing the differences between weighted and unweighted calculations.

### 1. Overview

The process involves several steps:

1.  **File Intake**: The API receives one or more `.ADM` log files via the `/api/DeathMap/generate` or `/api/DeathMap/generateWithMetadata` endpoints.
2.  **Configuration**: It uses the provided `DeathMapConfiguration`, including `MapName`, `OutputSize`, `OutputType` (`heat` or `pixel`), `BackgroundImage`, and the crucial `UseWeighting` flag.
3.  **Line Processing**: Each file is read line by line (`DeathMapService.GenerateHeatmapFromFilesAsync`).
4.  **Death Counting**: A specific check (`FileProcessingService.IsActualDeathEvent`) identifies lines representing actual player deaths to calculate the `TotalDeaths` value returned in the response. This count is *independent* of the weighting used for the map image.
5.  **Position Selection & Weighting**: A separate check (`FileProcessingService.ShouldIncludePositionForHeatmap`) determines if a line should contribute a *position* to the visual map and assigns a `Weight` based on the event type (e.g., zombie kill, long-distance kill, short-distance kill, other kill/death).
6.  **Coordinate Extraction**: If a line is selected for the map, its coordinates are extracted (`FileProcessingService.TryExtractPosition`).
7.  **Map Generation**: Based on the `OutputType`, either `GenerateHeatmapImageAsync` (for `heat`) or `GeneratePixelMapImageAsync` (for `pixel`) is called, using the collected list of `DeathPosition` objects and the configuration (including `UseWeighting`).
8.  **Response**: The generated image (and optionally metadata including `TotalDeaths`) is returned.

### 2. Heatmap Generation (`OutputType: "heat"`)

This method (`DeathMapService.GenerateHeatmapImageAsync`) creates a smooth heatmap effect using a Gaussian-like distribution for each death position.

1.  **Initialization**: An intensity grid (2D float array) matching the output image size is created and initialized to zeros. A background image is loaded (either custom, default map, or blank).
2.  **Intensity Calculation**: For each `DeathPosition` in the collected list:
    *   The X, Y coordinates are scaled to the output image size.
    *   A "dot size" and `sigma` (standard deviation for the Gaussian) are calculated based on the output size.
    *   A square region around the scaled position is iterated over.
    *   For each point (x, y) in this region, an intensity value is calculated based on its distance from the central death position using a formula resembling a Gaussian distribution: `Math.Exp(-distance_squared / (2 * sigma^2))`.
    *   **Weighting Logic**:
        *   The `config.UseWeighting` flag is checked.
        *   If `true`, the `position.Weight` (determined earlier by `ShouldIncludePositionForHeatmap`) is used. The code currently squares this weight (`weightFactor = (float)Math.Pow(weightToUse, 2);`) to give higher-weight events more visual impact.
        *   If `false`, a default weight factor of `1.0f` is used.
        *   The calculated Gaussian intensity is multiplied by this `weightFactor` before being added to the `intensityGrid[x, y]`.

    ```csharp
    // Inside GenerateHeatmapImageAsync loop:
    float weightToUse = config.UseWeighting ? position.Weight : 1.0f;
    float weightFactor = (float)Math.Pow(weightToUse, 2); // Square weight for impact
    intensityGrid[x, y] += (float)(Math.Exp(/*...gaussian calculation...*/)) * weightFactor;
    ```

3.  **Color Mapping**: The maximum intensity value in the grid is found. Each cell's intensity is normalized (divided by the max) to a 0.0-1.0 range. This value is used to pick a color from a predefined palette (e.g., black -> blue -> red -> white).
4.  **Overlay**: A transparent overlay image is created, and each pixel is colored based on the corresponding intensity grid cell's calculated color.
5.  **Final Image**: This heatmap overlay is drawn onto the background image with some transparency. The result is encoded as a PNG byte array.

### 3. Pixel Map Generation (`OutputType: "pixel"`)

This method (`DeathMapService.GeneratePixelMapImageAsync`) creates a grid-based map where each square's color represents the concentration of deaths within it.

1.  **Initialization**: A lower-resolution data grid (e.g., 100x100 float array) is created and initialized to zeros. A background image is loaded.
2.  **Data Grid Population**: For each `DeathPosition` in the collected list:
    *   The X, Y coordinates are scaled to the *data grid* size.
    *   **Weighting Logic**:
        *   The `config.UseWeighting` flag is checked.
        *   If `true`, the `position.Weight` is added to the corresponding cell in the `dataGrid`.
        *   If `false`, `1.0f` is added to the cell.

    ```csharp
    // Inside GeneratePixelMapImageAsync loop:
    if (posX >= 0 && posX < dataGridSize && posY >= 0 && posY < dataGridSize)
    {
        float valueToAdd = config.UseWeighting ? position.Weight : 1.0f;
        dataGrid[posY * dataGridSize + posX] += valueToAdd;
    }
    ```

3.  **Color Mapping**: The maximum value in the `dataGrid` is found. Each cell's value is compared to fractions of the maximum to determine its color (e.g., brown -> orange -> yellow -> white), or transparent if the value is zero.
4.  **Overlay**: A transparent overlay image (matching the final output size) is created. For each cell in the `dataGrid`, a corresponding square region in the overlay image is filled with the determined color.
5.  **Final Image**: This pixel overlay is drawn onto the background image with some transparency. The result is encoded as a PNG byte array.

### 4. Summary of Weighting Impact

*   **`UseWeighting = true`**:
    *   **Heatmap**: Events with higher weights (like long-distance kills) create brighter, more intense spots on the map compared to lower-weight events (like zombie kills).
    *   **Pixel Map**: Grid squares containing higher-weight events accumulate higher values faster, leading them to reach brighter colors (orange, yellow, white) with fewer actual events compared to squares with only low-weight events.
    *   **TotalDeaths**: Unaffected. Remains the count of actual death log lines identified.
*   **`UseWeighting = false`**:
    *   **Heatmap**: All events contribute equally to the intensity grid, regardless of how they occurred. The brightness purely reflects the density of events.
    *   **Pixel Map**: Each event adds `1.0` to its grid square. The color reflects the raw count of events within that square.
    *   **TotalDeaths**: Unaffected. Remains the count of actual death log lines identified.

## Setup & Running Locally

1.  **Prerequisites**: .NET 8 SDK installed.
2.  **Clone Repository**: `git clone <repository-url>`
3.  **Navigate**: `cd HeatMapAPI`
4.  **Restore Dependencies**: `dotnet restore`
5.  **Run**: `dotnet run`
6.  The API will typically be available at `https://localhost:7049` or `http://localhost:5243`. Check the console output for the exact URLs.
7.  Access the Swagger UI for interactive API testing (usually at `/swagger`).

## Usage Example (curl)

```bash
# Request a default (Namalsk, heat, unweighted) heatmap image
# Replace 'path/to/your.ADM' with the actual file path
curl -X POST "https://localhost:7049/api/DeathMap/generate" \
     -F "AdminLogFiles=@path/to/your.ADM" \
     -o heatmap.png --insecure

# Request a weighted pixel map for Livonia with metadata
# Configuration passed as a JSON string part
curl -X POST "https://localhost:7049/api/DeathMap/generateWithMetadata" \
     -F "AdminLogFiles=@path/to/another.ADM" \
     -F "Configuration={ "MapName": "Livonia", "OutputType": "pixel", "UseWeighting": true }" \
     --insecure
# (Output will be JSON)
```

*(Note: `--insecure` is used to bypass SSL certificate checks for localhost development. Remove if deploying with a valid certificate.)*

