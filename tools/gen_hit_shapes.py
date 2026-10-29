"""Builds the clickable shapes of every Spine skeleton used by a Capoo.

For every slot of every skeleton the script writes a small bitmap of the alpha
channel of the artwork plus the box it covers, so the game can tell if a click
landed on the character instead of on the empty space around it.

    python tools/gen_hit_shapes.py

Writes assets/data/hit_shapes.bin (see HitShapes.cs for the format).

The shapes live in the space of the skeleton, the same space the Spine runtime
draws the meshes in. The runtime of this project runs the skeletons y-down
(spine::Bone::setYDown(true)), so the root bone is mirrored on the y axis.
"""
import json
import math
import os
import struct
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPINE = os.path.join(ROOT, "assets", "spine")
OUT = os.path.join(ROOT, "assets", "data", "hit_shapes.bin")

# The folders whose skeletons are drawn on (or next to) a Capoo.
FOLDERS = [
    "body", "hat", "mask", "clothes", "back", "prop_r",
    "capoo_acc_top", "capoo_acc_body", "capoo_acc_bot", "face",
    "mood", "efx_top", "efx_bot",
]

MASK = 64          # the bitmap is MASK x MASK pixels
MAGIC = b"CAPOO_HITSHAPES2"
ALPHA = 128        # above this the pixel counts as artwork


# ---- Transforms ----
# A transform is (a, b, c, d, tx, ty) and maps a point with
#     x' = a * x + b * y + tx
#     y' = c * x + d * y + ty
# which is the convention of spine-cpp.

IDENTITY = (1.0, 0.0, 0.0, 1.0, 0.0, 0.0)


def apply(m, x, y):
    return (m[0] * x + m[1] * y + m[4], m[2] * x + m[3] * y + m[5])


def compose(parent, local):
    """The transform of a child inside its parent."""
    pa, pb, pc, pd, ptx, pty = parent
    la, lb, lc, ld, ltx, lty = local
    return (pa * la + pb * lc, pa * lb + pb * ld,
            pc * la + pd * lc, pc * lb + pd * ld,
            pa * ltx + pb * lty + ptx, pc * ltx + pd * lty + pty)


def invert(m):
    a, b, c, d, e, f = m
    det = a * d - c * b
    if abs(det) < 1e-12:
        return IDENTITY
    ia, ib, ic, id_ = d / det, -b / det, -c / det, a / det
    return (ia, ib, ic, id_, -(ia * e + ib * f), -(ic * e + id_ * f))


def rotation(angle):
    rad = math.radians(angle)
    cos, sin = math.cos(rad), math.sin(rad)
    return (cos, -sin, sin, cos, 0.0, 0.0)


def bone_worlds(bones, pose=None):
    """The world transform of every bone, the same way the runtime does it.

    The runtime mirrors the root bone on the y axis (y-down mode), so the
    skeleton space is y-down as well. When a pose is given it replaces the
    local values of the bones (x, y, rotation, scaleX, scaleY).
    """
    world = {}

    def build(name):
        if name in world:
            return world[name]
        bone = bones[name]
        if pose and name in pose:
            x, y, angle, scale_x, scale_y = pose[name]
            shear_x = bone.get("shearX", 0.0)
            shear_y = bone.get("shearY", 0.0)
        else:
            x = bone.get("x", 0.0)
            y = bone.get("y", 0.0)
            angle = bone.get("rotation", 0.0)
            scale_x = bone.get("scaleX", 1.0)
            scale_y = bone.get("scaleY", 1.0)
            shear_x = bone.get("shearX", 0.0)
            shear_y = bone.get("shearY", 0.0)
        parent = bone.get("parent")
        if parent:
            base = build(parent)
            local = (math.cos(math.radians(angle + shear_x)) * scale_x,
                     math.cos(math.radians(angle + 90 + shear_y)) * scale_y,
                     math.sin(math.radians(angle + shear_x)) * scale_x,
                     math.sin(math.radians(angle + 90 + shear_y)) * scale_y,
                     x, y)
            world[name] = compose(base, local)
        else:
            # The root bone inherits the scale of the skeleton: 1 on x, -1 on y.
            world[name] = (math.cos(math.radians(angle + shear_x)) * scale_x,
                           math.cos(math.radians(angle + 90 + shear_y)) * scale_y,
                           math.sin(math.radians(angle + shear_x)) * scale_x * -1.0,
                           math.sin(math.radians(angle + 90 + shear_y)) * scale_y * -1.0,
                           x, y * -1.0)
        return world[name]

    for name in bones:
        build(name)
    return world


# The animations the Capoo plays most of the time. The shapes are built for the
# average pose of these, so they match what the player sees.
# The animation that plays on a layer while the Capoo is idle. The shapes are built
# for the average pose of that animation, so they match what the player sees. The
# other layers play no animation and keep the pose of the skeleton.
REFERENCE_ANIMATIONS = {
    "body": "idle_basic",
    "face": "emoji",
    "hat": "animation",
}


def timeline_value(key, kind):
    """The value of one key of a bone timeline."""
    if kind == "rotate":
        return (key.get("angle", 0.0),)
    if kind == "scale":
        return (key.get("x", 1.0), key.get("y", 1.0))
    return (key.get("x", 0.0), key.get("y", 0.0))


def sample_timeline(keys, time, kind):
    """The value of a bone timeline at a time."""
    if not keys:
        return (0.0,)
    if time <= keys[0].get("time", 0.0):
        return timeline_value(keys[0], kind)
    if time >= keys[-1].get("time", 0.0):
        return timeline_value(keys[-1], kind)
    for i in range(len(keys) - 1):
        before = keys[i]
        after = keys[i + 1]
        start = before.get("time", 0.0)
        end = after.get("time", 0.0)
        if start <= time <= end:
            if before.get("curve") == "stepped":
                return timeline_value(before, kind)
            factor = 0.0 if end <= start else (time - start) / (end - start)
            first = timeline_value(before, kind)
            second = timeline_value(after, kind)
            return tuple(a + (b - a) * factor for a, b in zip(first, second))
    return timeline_value(keys[-1], kind)


def average_pose(data, folder, samples=24):
    """The average local pose of the bones while the reference animation plays."""
    animations = data.get("animations", {})
    name = REFERENCE_ANIMATIONS.get(folder)
    if name is None:
        return None
    animation = animations[name]

    timelines = {}
    duration = 0.0
    for bone_name, entries in animation.get("bones", {}).items():
        for kind, keys in entries.items():
            if not isinstance(keys, list) or not keys:
                continue
            timelines.setdefault(bone_name, {})[kind] = keys
            for key in keys:
                duration = max(duration, key.get("time", 0.0))
    if duration <= 0:
        return None

    totals = {}
    for step in range(samples):
        time = duration * (step + 0.5) / samples
        for bone in data.get("bones", []):
            name = bone["name"]
            entries = timelines.get(name, {})
            x = bone.get("x", 0.0)
            y = bone.get("y", 0.0)
            rotation = bone.get("rotation", 0.0)
            scale_x = bone.get("scaleX", 1.0)
            scale_y = bone.get("scaleY", 1.0)
            if "translate" in entries:
                move_x, move_y = sample_timeline(entries["translate"], time, "translate")
                x += move_x
                y += move_y
            if "rotate" in entries:
                rotation += sample_timeline(entries["rotate"], time, "rotate")[0]
            if "scale" in entries:
                factor_x, factor_y = sample_timeline(entries["scale"], time, "scale")
                scale_x *= factor_x
                scale_y *= factor_y
            total = totals.setdefault(name, [0.0, 0.0, 0.0, 0.0, 0.0])
            total[0] += x
            total[1] += y
            total[2] += rotation
            total[3] += scale_x
            total[4] += scale_y
    return {name: [value / samples for value in values] for name, values in totals.items()}


# ---- Atlas ----

def read_atlas(path):
    """Parses a Spine atlas: the size of the page and its regions."""
    blocks = []
    name = None
    props = {}
    with open(path, encoding="utf-8") as handle:
        for raw in handle:
            line = raw.strip()
            if not line:
                continue
            # The atlas files of this project are not indented: a line with a colon
            # belongs to the entry above it, everything else starts a new entry.
            if ":" in line:
                key, _, value = line.partition(":")
                props[key.strip()] = value.strip()
                continue
            if name is not None:
                blocks.append((name, props))
            name = line
            props = {}
    if name is not None:
        blocks.append((name, props))

    if not blocks:
        return (0, 0), {}, ""

    page_size = (0, 0)
    if "size" in blocks[0][1]:
        parts = [int(v) for v in blocks[0][1]["size"].split(",")]
        page_size = (parts[0], parts[1])

    regions = {}
    for region_name, props in blocks[1:]:
        region = {"x": 0, "y": 0, "w": 0, "h": 0, "ow": 0, "oh": 0,
                  "ox": 0, "oy": 0, "degrees": 0}
        if "bounds" in props:
            parts = [int(v) for v in props["bounds"].split(",")]
            region["x"], region["y"], region["w"], region["h"] = parts
        if "xy" in props:
            parts = [int(v) for v in props["xy"].split(",")]
            region["x"], region["y"] = parts[0], parts[1]
        if "size" in props:
            parts = [int(v) for v in props["size"].split(",")]
            region["w"], region["h"] = parts[0], parts[1]
        if "offsets" in props:
            parts = [int(v) for v in props["offsets"].split(",")]
            region["ox"], region["oy"], region["ow"], region["oh"] = parts
        if "orig" in props:
            parts = [int(v) for v in props["orig"].split(",")]
            region["ow"], region["oh"] = parts[0], parts[1]
        if "offset" in props:
            parts = [int(v) for v in props["offset"].split(",")]
            region["ox"], region["oy"] = parts[0], parts[1]
        if "rotate" in props:
            value = props["rotate"]
            region["degrees"] = 90 if value in ("true", "90") else int(value or 0)
        if region["ow"] == 0:
            region["ow"] = region["w"]
        if region["oh"] == 0:
            region["oh"] = region["h"]
        if region["w"] == 0 or region["h"] == 0:
            continue
        regions[region_name] = region
    return page_size, regions, blocks[0][0]


def region_uvs(region, page_size):
    """The corners of a region in the page, the way the runtime maps them."""
    page_width, page_height = page_size
    u = region["x"] / page_width
    v = region["y"] / page_height
    if region["degrees"] == 90:
        u2 = (region["x"] + region["h"]) / page_width
        v2 = (region["y"] + region["w"]) / page_height
    else:
        u2 = (region["x"] + region["w"]) / page_width
        v2 = (region["y"] + region["h"]) / page_height
    return (u, v, u2, v2)


def attachment_uvs(region, page_size, uv):
    """Maps the normalized coordinates of an attachment onto the page.

    Same calculation as the runtime (MeshAttachment::updateRegion), so the
    artwork is sampled where it really is.
    """
    u0, v0, u1, v1 = region_uvs(region, page_size)
    if region["degrees"] == 90:
        texture_width = region["h"] / (u1 - u0)
        texture_height = region["w"] / (v1 - v0)
        u = u0 - (region["oh"] - region["oy"] - region["h"]) / texture_width
        v = v0 - (region["ow"] - region["ox"] - region["w"]) / texture_height
        width = region["oh"] / texture_width
        height = region["ow"] / texture_height
        return (u + uv[1] * width, v + (1.0 - uv[0]) * height)
    texture_width = region["w"] / (u1 - u0)
    texture_height = region["h"] / (v1 - v0)
    u = u0 - region["ox"] / texture_width
    v = v0 - (region["oh"] - region["oy"] - region["h"]) / texture_height
    width = region["ow"] / texture_width
    height = region["oh"] / texture_height
    return (u + uv[0] * width, v + uv[1] * height)


# ---- Shapes ----

class Shape:
    """The clickable shape of one attachment: a box and a bitmap of it."""

    def __init__(self, box, bitmap):
        self.box = box           # (left, bottom, right, top)
        self.bitmap = bitmap     # MASK x MASK bytes, 0 or 255


def mesh_shape(points, triangles, uvs, region, page_size, alpha_page):
    """The shape of a mesh attachment: its triangles, filled with the artwork."""
    if not points or not triangles or not region:
        return None
    box = (min(p[0] for p in points), min(p[1] for p in points),
           max(p[0] for p in points), max(p[1] for p in points))
    left, bottom, right, top = box
    width = right - left
    height = top - bottom
    if width <= 0 or height <= 0:
        return None

    bitmap = bytearray(MASK * MASK)
    cell_width = width / MASK
    cell_height = height / MASK
    # Only the part of the page that belongs to the region is artwork.
    u0, v0, u1, v1 = region_uvs(region, page_size)
    edge = 0.5 / page_size[0], 0.5 / page_size[1]

    for index in range(0, len(triangles), 3):
        a, b, c = triangles[index], triangles[index + 1], triangles[index + 2]
        if a >= len(points) or b >= len(points) or c >= len(points):
            continue
        pa, pb, pc = points[a], points[b], points[c]
        ua = attachment_uvs(region, page_size, (uvs[a * 2], uvs[a * 2 + 1]))
        ub = attachment_uvs(region, page_size, (uvs[b * 2], uvs[b * 2 + 1]))
        uc = attachment_uvs(region, page_size, (uvs[c * 2], uvs[c * 2 + 1]))

        min_col = max(0, int((min(pa[0], pb[0], pc[0]) - left) / cell_width))
        max_col = min(MASK - 1, int((max(pa[0], pb[0], pc[0]) - left) / cell_width))
        min_row = max(0, int((min(pa[1], pb[1], pc[1]) - bottom) / cell_height))
        max_row = min(MASK - 1, int((max(pa[1], pb[1], pc[1]) - bottom) / cell_height))

        for row in range(min_row, max_row + 1):
            y = bottom + (row + 0.5) * cell_height
            for col in range(min_col, max_col + 1):
                x = left + (col + 0.5) * cell_width
                weights = barycentric(x, y, pa, pb, pc)
                if weights is None:
                    continue
                wa, wb, wc = weights
                u = ua[0] * wa + ub[0] * wb + uc[0] * wc
                v = ua[1] * wa + ub[1] * wb + uc[1] * wc
                if u < u0 - edge[0] or u > u1 + edge[0]:
                    continue
                if v < v0 - edge[1] or v > v1 + edge[1]:
                    continue
                if alpha_at(alpha_page, page_size, u, v) >= ALPHA:
                    bitmap[row * MASK + col] = 255
    return Shape(box, bitmap)


def region_shape(attachment_box, uvs, region, page_size, alpha_page, bone_matrix):
    """The shape of a region attachment: the artwork inside its corners."""
    if not attachment_box or not region:
        return None
    local_x, local_y, local_x2, local_y2 = attachment_box["box"]
    placement = attachment_box["placement"]
    corners = []
    for lx, ly in ((local_x, local_y), (local_x2, local_y),
                   (local_x, local_y2), (local_x2, local_y2)):
        px, py = apply(placement, lx, ly)
        corners.append(apply(bone_matrix, px, py))
    box = (min(p[0] for p in corners), min(p[1] for p in corners),
           max(p[0] for p in corners), max(p[1] for p in corners))
    left, bottom, right, top = box
    width = right - left
    height = top - bottom
    if width <= 0 or height <= 0:
        return None

    bitmap = bytearray(MASK * MASK)
    cell_width = width / MASK
    cell_height = height / MASK
    world_to_local = compose(invert(placement), invert(bone_matrix))

    span_x = local_x2 - local_x
    span_y = local_y2 - local_y
    if span_x == 0 or span_y == 0:
        return None

    for row in range(MASK):
        y = bottom + (row + 0.5) * cell_height
        for col in range(MASK):
            x = left + (col + 0.5) * cell_width
            local = apply(world_to_local, x, y)
            u = (local[0] - local_x) / span_x
            v = (local[1] - local_y) / span_y
            if u < 0 or u > 1 or v < 0 or v > 1:
                continue
            # The texture coordinates follow the attachment, not the page: a rotated
            # region puts the page's x on the attachment's y.
            tex_u = uvs[0][0] + u * (uvs[1][0] - uvs[0][0]) + v * (uvs[2][0] - uvs[0][0])
            tex_v = uvs[0][1] + u * (uvs[1][1] - uvs[0][1]) + v * (uvs[2][1] - uvs[0][1])
            if alpha_at(alpha_page, page_size, tex_u, tex_v) >= ALPHA:
                bitmap[row * MASK + col] = 255
    return Shape(box, bitmap)


def barycentric(x, y, a, b, c):
    """The weights of a point inside a triangle, None when it is outside."""
    # p = a + w_b * (b - a) + w_c * (c - a)
    v0 = (b[0] - a[0], b[1] - a[1])
    v1 = (c[0] - a[0], c[1] - a[1])
    v2 = (x - a[0], y - a[1])
    den = v0[0] * v1[1] - v1[0] * v0[1]
    if abs(den) < 1e-9:
        return None
    weight_b = (v2[0] * v1[1] - v1[0] * v2[1]) / den
    weight_c = (v0[0] * v2[1] - v2[0] * v0[1]) / den
    weight_a = 1.0 - weight_b - weight_c
    if weight_a < -1e-6 or weight_b < -1e-6 or weight_c < -1e-6:
        return None
    return (weight_a, weight_b, weight_c)


def alpha_at(page, page_size, u, v):
    """The alpha of a point of the page, 0 outside."""
    if page is None:
        return 0
    page_width, page_height = page_size
    x = int(u * page_width)
    y = int(v * page_height)
    if x < 0 or y < 0 or x >= page.size[0] or y >= page.size[1]:
        return 0
    return page.getpixel((x, y))[3]


# ---- One skeleton ----

def parse_mesh_vertices(values, bone_names, vertex_count):
    """The vertices of a mesh: a list of (bone, x, y, weight) lists.

    A mesh that is not weighted to bones keeps the plain positions of its vertices.
    """
    if len(values) == vertex_count * 2:
        return [[(None, values[i * 2], values[i * 2 + 1], 1.0)] for i in range(vertex_count)]
    result = []
    index = 0
    while index < len(values):
        count = int(values[index])
        index += 1
        influences = []
        for _ in range(count):
            if index + 3 >= len(values):
                break
            bone = int(values[index])
            influences.append((bone_names[bone] if 0 <= bone < len(bone_names) else None,
                               values[index + 1], values[index + 2], values[index + 3]))
            index += 4
        result.append(influences)
    return result


def region_corners(attachment, region, page_size):
    """The box of a region attachment in the space of its bone."""
    width = attachment.get("width", 0.0)
    height = attachment.get("height", 0.0)
    scale_x = attachment.get("scaleX", 1.0)
    scale_y = attachment.get("scaleY", 1.0)
    x = attachment.get("x", 0.0)
    y = attachment.get("y", 0.0)
    angle = attachment.get("rotation", 0.0)

    region_scale_x = width / region["ow"] * scale_x if region["ow"] else scale_x
    region_scale_y = height / region["oh"] * scale_y if region["oh"] else scale_y
    local_x = -width / 2 * scale_x + region["ox"] * region_scale_x
    local_y = -height / 2 * scale_y + region["oy"] * region_scale_y
    local_x2 = local_x + region["w"] * region_scale_x
    local_y2 = local_y + region["h"] * region_scale_y

    u0, v0, u1, v1 = region_uvs(region, page_size)
    # The runtime keeps the texture coordinates of a region in the order
    # bottom-left, top-left, top-right, bottom-right of the page.
    if region["degrees"] == 90:
        uvs = [(u1, v1), (u1, v0), (u0, v1), (u0, v0)]
    else:
        uvs = [(u0, v1), (u1, v1), (u0, v0), (u1, v0)]

    placement = compose((1.0, 0.0, 0.0, 1.0, x, y), rotation(angle))
    return ({"box": (local_x, local_y, local_x2, local_y2),
             "placement": placement}, uvs)


def build_skeleton(directory, skeleton_name, atlas_path, folder):
    """All the shapes of one skeleton, by slot and attachment."""
    with open(os.path.join(directory, skeleton_name + ".spine-json"), encoding="utf-8") as handle:
        data = json.load(handle)

    bones = {bone["name"]: bone for bone in data.get("bones", [])}
    bone_names = [bone["name"] for bone in data.get("bones", [])]
    world = bone_worlds(bones, average_pose(data, folder))

    page_size, regions, page_image = read_atlas(atlas_path)
    alpha_page = None
    image_path = os.path.join(directory, page_image)
    if os.path.exists(image_path):
        alpha_page = Image.open(image_path).convert("RGBA")

    # The attachments of every slot.
    skins = data.get("skins", [])
    attachments = {}
    skin_list = skins if isinstance(skins, list) else list(skins.values())
    for skin in skin_list:
        for slot_name, slot_attachments in skin.get("attachments", {}).items():
            attachments.setdefault(slot_name, {}).update(slot_attachments)

    slots = []
    for slot in data.get("slots", []):
        slot_name = slot["name"]
        bone_matrix = world.get(slot["bone"])
        shapes = {}
        if bone_matrix is None:
            slots.append(shapes)
            continue
        for attachment_name, attachment in attachments.get(slot_name, {}).items():
            # Without a type the attachment is a region, the way the runtime reads it.
            kind = attachment.get("type", "region")
            if kind not in ("mesh", "region"):
                continue
            region = regions.get(attachment.get("path", attachment_name))
            if region is None:
                continue
            shape = None
            if kind == "mesh":
                uvs = attachment.get("uvs", [])
                influences = parse_mesh_vertices(attachment.get("vertices", []), bone_names,
                                                 len(uvs) // 2)
                points = []
                for vertex in influences:
                    x = y = 0.0
                    for bone, vx, vy, weight in vertex:
                        matrix = world.get(bone) if bone else bone_matrix
                        if matrix is None:
                            continue
                        px, py = apply(matrix, vx, vy)
                        x += px * weight
                        y += py * weight
                    points.append((x, y))
                shape = mesh_shape(points, attachment.get("triangles", []),
                                   attachment.get("uvs", []), region,
                                   page_size, alpha_page)
            else:
                local_corners, uvs = region_corners(attachment, region, page_size)
                shape = region_shape(local_corners, uvs, region, page_size,
                                     alpha_page, bone_matrix)
            if shape is None:
                continue
            shapes[attachment_name] = shape
        # Every slot is written, so the runtime can find the shapes by index.
        slots.append(shapes)
    return slots


def pack_mask(bitmap):
    """The bitmap as bits, one bit per pixel, the bottom row first."""
    out = bytearray(MASK * MASK // 8)
    for row in range(MASK):
        for col in range(MASK):
            if bitmap[row * MASK + col]:
                out[row * (MASK // 8) + col // 8] |= 1 << (col % 8)
    return bytes(out)


def main():
    all_shapes = {}
    for folder in FOLDERS:
        directory = os.path.join(SPINE, folder)
        if not os.path.isdir(directory):
            continue
        count = 0
        for file_name in sorted(os.listdir(directory)):
            if not file_name.endswith(".spine-json"):
                continue
            skeleton_name = file_name[:-len(".spine-json")]
            atlas_path = os.path.join(directory, skeleton_name + ".atlas")
            if not os.path.exists(atlas_path):
                continue
            slots = build_skeleton(directory, skeleton_name, atlas_path, folder)
            if slots:
                all_shapes[skeleton_name] = slots
                count += 1
        print(f"{folder}: {count} skeletons")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "wb") as handle:
        handle.write(MAGIC)
        handle.write(struct.pack("<I", len(all_shapes)))
        for skeleton_name in sorted(all_shapes):
            slots = all_shapes[skeleton_name]
            encoded = skeleton_name.encode("utf-8")
            handle.write(struct.pack("<H", len(encoded)))
            handle.write(encoded)
            handle.write(struct.pack("<H", len(slots)))
            for shapes in slots:
                handle.write(struct.pack("<H", len(shapes)))
                for attachment_name in sorted(shapes):
                    shape = shapes[attachment_name]
                    encoded = attachment_name.encode("utf-8")
                    handle.write(struct.pack("<H", len(encoded)))
                    handle.write(encoded)
                    handle.write(struct.pack("<4f", *shape.box))
                    handle.write(pack_mask(shape.bitmap))
    print("wrote", OUT, os.path.getsize(OUT), "bytes")


if __name__ == "__main__":
    sys.exit(main())
