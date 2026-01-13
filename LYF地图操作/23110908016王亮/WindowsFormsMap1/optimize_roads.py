import json
import os

# CONFIGURATION
INPUT_FILE = 'VisualWeb/data/370000.geojson'
OUTPUT_FILE = 'VisualWeb/data/roads.json'
# We keep ONLY highest level highways for perfect performance (180MB -> <5MB target)
KEEP_CLASSES = {'motorway', 'motorway_link', 'trunk', 'trunk_link'}

def simplify_coords(coords, threshold=0.008):
    """Simple radial distance simplification to reduce vertex count while keeping curves."""
    if not coords or len(coords) < 3:
        return coords
    
    new_coords = [coords[0]]
    last_p = coords[0]
    
    for i in range(1, len(coords) - 1):
        p = coords[i]
        # Calculate approximate Euclidean distance (sq)
        dist_sq = (p[0] - last_p[0])**2 + (p[1] - last_p[1])**2
        if dist_sq > threshold**2:
            new_coords.append(p)
            last_p = p
            
    new_coords.append(coords[-1])
    return new_coords

def process():
    print(f"Starting optimization of {INPUT_FILE}...")
    if not os.path.exists(INPUT_FILE):
        print("Input file not found.")
        return

    features_out = []
    count = 0
    saved = 0

    try:
        with open(INPUT_FILE, 'r', encoding='utf-8') as f:
            data = json.load(f)
            
        for feature in data.get('features', []):
            count += 1
            props = feature.get('properties', {})
            fclass = props.get('fclass')
            
            if fclass in KEEP_CLASSES:
                geom = feature.get('geometry', {})
                base_props = {
                    'name': props.get('name'),
                    'fclass': fclass,
                    'ref': props.get('ref')
                }

                if geom['type'] == 'MultiLineString':
                    for ls in geom['coordinates']:
                        simple_ls = simplify_coords(ls)
                        if len(simple_ls) >= 2:
                            features_out.append({
                                "type": "Feature",
                                "properties": base_props,
                                "geometry": {"type": "LineString", "coordinates": simple_ls}
                            })
                            saved += 1
                elif geom['type'] == 'LineString':
                    simple_ls = simplify_coords(geom['coordinates'])
                    if len(simple_ls) >= 2:
                        features_out.append({
                            "type": "Feature",
                            "properties": base_props,
                            "geometry": {"type": "LineString", "coordinates": simple_ls}
                        })
                        saved += 1
                
        output_data = {
            "type": "FeatureCollection",
            "features": features_out
        }
        
        with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
            json.dump(output_data, f, separators=(',', ':'), ensure_ascii=False)
            
        print(f"Finished! Processed {count} features, kept {saved} major roads.")
        print(f"Output saved to {OUTPUT_FILE}")
        
    except Exception as e:
        print(f"Error during processing: {e}")

if __name__ == "__main__":
    process()
