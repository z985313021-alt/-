
import struct
import json
import os

def read_dbf(dbf_path):
    # Minimal DBF reader
    # Returns list of dicts
    with open(dbf_path, 'rb') as f:
        # Header
        header = f.read(32)
        num_records = struct.unpack('<I', header[4:8])[0]
        header_len = struct.unpack('<H', header[8:10])[0]
        record_len = struct.unpack('<H', header[10:12])[0]
        
        # Fields
        fields = []
        while f.tell() < header_len - 1:
            field_data = f.read(32)
            if len(field_data) < 32: break
            name = field_data[0:10].split(b'\x00')[0].decode('gbk', 'ignore') 
            fields.append(name)
            
        f.seek(header_len)
        records = []
        for _ in range(num_records):
            record = {}
            # Delete flag
            f.read(1)
            for i, field_name in enumerate(fields):
                # We assume simplified logic here. 
                # In robust reader we use field lengths from header, but let's try to just dump raw first or guess.
                # Actually, we MUST read field definition to know length.
                pass
            # This naive approach is too risky without field lengths.
            # Let's just create a robust-ish reader.
            records.append(record)
    return fields

# Better approach: Just print the header info first to debug.
if __name__ == "__main__":
    dbf_path = r"c:\Users\24147\xwechat_files\wxid_dm4f57ypxzt722_21a2\msg\file\2026-01\ARCGIS\ARCGIS\曲艺非遗项目.dbf"
    shp_path = r"c:\Users\24147\xwechat_files\wxid_dm4f57ypxzt722_21a2\msg\file\2026-01\ARCGIS\ARCGIS\曲艺非遗项目.shp"
    
    try:
        with open(dbf_path, 'rb') as f:
            header = f.read(32)
            num_records = struct.unpack('<I', header[4:8])[0]
            header_len = struct.unpack('<H', header[8:10])[0]
            record_len = struct.unpack('<H', header[10:12])[0]
            print(f"DBF: {num_records} records, header_len={header_len}, record_len={record_len}")
            
            # Read fields
            fields = []
            while f.tell() < header_len - 1:
                bytes_ = f.read(32)
                if bytes_[0] == 0x0D: break
                name = bytes_[0:11].split(b'\x00')[0].decode('gbk', 'ignore').strip()
                length = bytes_[16]
                fields.append({'name': name, 'length': length})
            print("Fields:", [x['name'] for x in fields])
            
            # Read first 5 records
            f.seek(header_len)
            for i in range(5):
                rec_bytes = f.read(record_len)
                print(f"Record {i}: {rec_bytes}")
                
        with open(shp_path, 'rb') as f:
            header = f.read(100)
            # file length in 16-bit words
            file_len = struct.unpack('>I', header[24:28])[0] * 2
            print(f"SHP File size: {file_len} bytes")
            
            # Read first records
            f.seek(100)
            for i in range(5):
                rec_header = f.read(8)
                if len(rec_header) < 8: break
                rec_num, content_len = struct.unpack('>II', rec_header)
                content_len = content_len * 2
                
                content = f.read(content_len)
                shape_type = struct.unpack('<I', content[0:4])[0]
                x, y = struct.unpack('<dd', content[4:20])
                print(f"Point {i}: Type={shape_type}, X={x}, Y={y}")
                
    except Exception as e:
        print(f"Error: {e}")
