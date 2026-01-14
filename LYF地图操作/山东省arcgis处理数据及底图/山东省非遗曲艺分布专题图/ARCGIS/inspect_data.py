
import geopandas as gpd

base_map_path = r"c:\Users\24147\xwechat_files\wxid_dm4f57ypxzt722_21a2\msg\file\2026-01\ARCGIS\ARCGIS\山东省_市.shp"
points_path = r"c:\Users\24147\xwechat_files\wxid_dm4f57ypxzt722_21a2\msg\file\2026-01\ARCGIS\ARCGIS\曲艺非遗项目.shp"

try:
    print("--- Base Map Attributes ---")
    gdf_base = gpd.read_file(base_map_path)
    print(gdf_base.head())
    print(gdf_base.columns)

    print("\n--- Points Attributes ---")
    gdf_points = gpd.read_file(points_path)
    print(gdf_points[['名称', '项目序', '类别', '申报地'] if '名称' in gdf_points.columns else gdf_points.columns].head(20))
except Exception as e:
    print(f"Error reading shapefiles: {e}")
