#!/usr/bin/env python3
import sys
import struct
import json
import lzma
from datetime import datetime, timedelta

# Minimal parser for .osr (legacy lazer format) to JSON.
# Usage: python osr_to_json.py input.osr output.json

FIRST_LAZER_VERSION = 20180101  # used only for conditional logic in original code; actual constant differs

class Reader:
    def __init__(self, f):
        self.f = f

    def read(self, n):
        data = self.f.read(n)
        if len(data) != n:
            raise EOFError("unexpected EOF")
        return data

    def read_byte(self):
        return struct.unpack('<B', self.read(1))[0]

    def read_bool(self):
        return struct.unpack('<?', self.read(1))[0]

    def read_int16(self):
        return struct.unpack('<h', self.read(2))[0]

    def read_uint16(self):
        return struct.unpack('<H', self.read(2))[0]

    def read_int32(self):
        return struct.unpack('<i', self.read(4))[0]

    def read_uint32(self):
        return struct.unpack('<I', self.read(4))[0]

    def read_int64(self):
        return struct.unpack('<q', self.read(8))[0]

    def read_uint64(self):
        return struct.unpack('<Q', self.read(8))[0]

    def read_single(self):
        return struct.unpack('<f', self.read(4))[0]

    def read_double(self):
        return struct.unpack('<d', self.read(8))[0]

    def read_7bit_encoded_int(self):
        # mirror .NET BinaryReader.Read7BitEncodedInt
        result = 0
        shift = 0
        while True:
            b = self.read_byte()
            result |= (b & 0x7f) << shift
            if (b & 0x80) == 0:
                break
            shift += 7
            if shift > 35:
                raise ValueError("Too many bytes for 7bit int")
        return result

    def read_string(self):
        # mirror SerializationReader.ReadString: first a single byte flag, 0 => null
        flag = self.read_byte()
        if flag == 0:
            return None
        # otherwise the next data is a .NET length-prefixed UTF8 string (7-bit encoded length)
        length = self.read_7bit_encoded_int()
        if length == 0:
            return ""
        raw = self.read(length)
        return raw.decode('utf-8')

    def read_byte_array(self):
        length = self.read_int32()
        if length > 0:
            return self.read(length)
        if length < 0:
            return None
        return b''

    def read_datetime(self):
        ticks = self.read_int64()
        if ticks < 0:
            raise IOError('Bad ticks count read!')
        # .NET ticks are 100-nanoseconds since 0001-01-01
        # convert to ISO8601 UTC string
        # caution: Python's datetime can handle year >= 1
        dt = datetime(1,1,1) + timedelta(microseconds=ticks/10)
        return dt.isoformat() + 'Z'


def parse_osr(path):
    out = {}
    with open(path, 'rb') as f:
        r = Reader(f)
        try:
            ruleset_id = r.read_byte()
        except EOFError:
            raise
        out['ruleset_id'] = ruleset_id

        version = r.read_int32()
        out['version'] = version

        beatmap_hash = r.read_string()
        out['beatmap_hash'] = beatmap_hash

        username = r.read_string()
        out['username'] = username

        md5hash = r.read_string()
        out['md5hash'] = md5hash

        counts = {}
        counts['count300'] = r.read_uint16()
        counts['count100'] = r.read_uint16()
        counts['count50'] = r.read_uint16()
        counts['countGeki'] = r.read_uint16()
        counts['countKatu'] = r.read_uint16()
        counts['countMiss'] = r.read_uint16()
        out['counts'] = counts

        out['total_score'] = r.read_int32()
        out['max_combo'] = r.read_uint16()

        out['perfect'] = r.read_bool()

        out['mods_legacy'] = r.read_int32()

        # hp graph string
        out['hp_graph'] = r.read_string()

        out['date_utc'] = r.read_datetime()

        compressed_replay = r.read_byte_array()
        out['compressed_replay_length'] = None if compressed_replay is None else len(compressed_replay)

        # legacy online id handling
        legacy_online_id = None
        if version >= 20140721:
            legacy_online_id = r.read_int64()
        elif version >= 20121008:
            legacy_online_id = r.read_int32()
        out['legacy_online_id'] = legacy_online_id if legacy_online_id != 0 else -1

        compressed_score_info = None
        if version >= 30000001:
            compressed_score_info = r.read_byte_array()
            out['compressed_score_info_length'] = None if compressed_score_info is None else len(compressed_score_info)
        else:
            out['compressed_score_info_length'] = None

        # parse replay frames if available
        out['replay_frames'] = []

        if compressed_replay and len(compressed_replay) > 0:
            try:
                decompressed = lzma.decompress(compressed_replay, format=lzma.FORMAT_ALONE)
            except Exception:
                # fallback: sometimes the data is stored without the 13-byte header - try using raw
                try:
                    dec = lzma.LZMADecompressor(format=lzma.FORMAT_ALONE)
                    decompressed = dec.decompress(compressed_replay)
                except Exception as e:
                    out['replay_error'] = f"lzma decompress failed: {e}"
                    decompressed = b''

            text = decompressed.decode('utf-8', errors='ignore')
            # frames separated by comma
            parts = text.split(',')
            last_time = 0
            frames = []
            for p in parts:
                if not p:
                    continue
                split = p.split('|')
                if len(split) < 4:
                    continue
                if split[0] == '-12345':
                    # seed line, ignore
                    continue
                # parse delta (int preferred)
                diff = None
                try:
                    diff = int(split[0])
                except Exception:
                    try:
                        diff = int(round(float(split[0])))
                    except Exception:
                        diff = 0
                # parse mouse positions
                try:
                    mouse_x = float(split[1])
                except Exception:
                    mouse_x = None
                try:
                    mouse_y = float(split[2])
                except Exception:
                    mouse_y = None
                try:
                    button_state = int(split[3])
                except Exception:
                    button_state = 0

                last_time += diff
                frames.append({
                    'time': last_time,
                    'mouse_x': mouse_x,
                    'mouse_y': mouse_y,
                    'button_state': button_state
                })

            out['replay_frames'] = frames

        # parse compressed score info if present (it's JSON text inside LZMA)
        out['score_info'] = None
        if compressed_score_info and len(compressed_score_info) > 0:
            try:
                decompressed = lzma.decompress(compressed_score_info, format=lzma.FORMAT_ALONE)
                txt = decompressed.decode('utf-8', errors='ignore')
                # legacy decoder expects JSON which contains fields like OnlineID, Statistics, Pauses etc.
                try:
                    parsed = json.loads(txt)
                except Exception:
                    parsed = {'_raw': txt}
                out['score_info'] = parsed
            except Exception as e:
                out['score_info_error'] = f"lzma decompress failed: {e}"

    return out

if __name__ == '__main__':
    if len(sys.argv) < 3:
        print('Usage: osr_to_json.py input.osr output.json')
        sys.exit(1)
    inpath = sys.argv[1]
    outpath = sys.argv[2]
    parsed = parse_osr(inpath)
    with open(outpath, 'w', encoding='utf-8') as of:
        json.dump(parsed, of, ensure_ascii=False, indent=2)
    print(f'Wrote {outpath}')
