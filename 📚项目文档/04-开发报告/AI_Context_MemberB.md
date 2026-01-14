# SYSTEM CONTEXT: Member B Dashboard Module

> **Target Audience**: AI Agents / Copilots  
> **Purpose**: Rapidly understand module structure, dependencies, and integration points for future refactoring or debugging.

## 1. Module Overview
*   **Role**: Member B (Dashboard & Statistics)
*   **Namespace**: `WindowsFormsMap1`
*   **Primary Form**: `FormChart.cs` (Inherits `Form`)
*   **Integration Point**: `Form1.Visual.cs` (`InitVisualLayout` method)

## 2. File Topology

### 2.1 [Core UI] `FormChart.cs`
*   **Controls**:
    *   `chart1` (System.Windows.Forms.DataVisualization.Charting.Chart): Displays "Heritage Count by City".
    *   `trackBar1` (System.Windows.Forms.TrackBar): Year selector (Range: [Min, Max]).
    *   `lblTime` (Label): Displays current selected year.
*   **Key Methods**:
    *   `InitMyChart()`: Hardcoded data population for 16 Shandong cities.
    *   `UpdateChartData(int year)`: Generates simulated data using `Base + Trend + Noise` algorithm.
    *   `SetMapControl(AxMapControl)`: Dependency injection for map linkage.
    *   `ZoomToCity(city)`: Implements smart layer routing (Polygon priority -> Point fallback) and visual feedback (Zoom/Flash).
    *   `trackBar1_Scroll`: Coordinates both `UpdateChartData` and `FilterMapByYear`.

### 2.2 [Integration Logic] `Form1.Visual.cs`
*   **Method**: `InitVisualLayout()`
*   **Logic**:
    *   Dynamically instantiates `SplitContainer`.
    *   Re-parents `axMapControlVisual` to `Panel1`.
    *   Embeds `FormChart` (TopLevel=false) into `Panel2`.
*   **Trigger**: `TabControl1_SelectedIndexChanged` (When index == Visual Tab).
*   **Map Interaction**: Implements `FilterMapByYear(year)` using `IFeatureLayerDefinition.DefinitionExpression` for real-time GIS feature display control.

### 2.3 [Legacy/Backup] `Form1.Editor.cs`
*   **Method**: `InitDashboardModule()`
*   **Status**: Deprecated/Backup. Used for standalone popup mode if embedded mode fails.

## 3. Data Flow
1.  **User Action**: User switches to "Visual Presentation" tab.
2.  **Trigger**: `Form1` calls `InitVisualLayout()`.
3.  **UI Construction**: `FormChart` is instantiated and docked to bottom.
4.  **Interaction**: User slides `trackBar1`.
5.  **Event**: `trackBar1_Scroll`.
6.  **Real Calculation**: `UpdateChartData(year)` calls `Form1.GetCountByCity(city, year)` to trigger actual SQL query.
7.  **Map Synchronization**: `FormChart` calls `Form1.FilterMapByYear(year)` to apply dynamic GIS feature visibility filtering.
8.  **Data Logic**: The logic matches the `公布时间` field and applies a "Year vs Batch" dual-mode check for true temporal filtering.
9.  **Interactive Logic**: `ZoomToCity` uses a regex-like layer name matching + GeometryType check to resolve the "Polygon vs Point" ambiguity during city localization.

## 4. Final Features & Status
*   **Spatio-temporal Integrity**: Fully integrated with `Form1.Data.cs` to fetch real GIS attribute counts.
*   **Double-Type SQL Safety**: Query logic detects Numeric vs String fields to apply correct SQL syntax.
*   **Dual-Mode Field Mapping**: Automatically maps Year (e.g. 2006) to Batch (e.g. 1) to handle diverse SHP schemas.
*   **Project Link Restored**: `.csproj` manually fixed to include `Form1.Data.cs` and related partial classes.

## 5. Compliance
*   All code authored by Member B includes `// [Member B]` header.
*   No logic placed in `Form1.cs` directly (except necessary event hooks).
