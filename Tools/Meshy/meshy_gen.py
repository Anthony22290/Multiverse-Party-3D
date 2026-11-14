"""Genera un personaje con Meshy: preview (meshy-6-lite) -> textura -> rig + animaciones basicas.
Reanudable: guarda el estado en <out>/<nombre>/state.json para no repetir pasos (ni gastar creditos dos veces).
Uso: python3 meshy_gen.py <nombre>   (lee la ficha de characters.json)"""
import json, os, sys, time, requests

ROOT = os.path.dirname(os.path.abspath(__file__))
API = "https://api.meshy.ai/openapi"

def key():
    for line in open(os.path.join(ROOT, "..", "ai3d_keys.env")):
        if line.startswith("MESHY_API_KEY="): return line.split("=", 1)[1].strip()

H = {"Authorization": "Bearer " + key()}
STYLE = ("Stylized low-poly cartoon video game character, chunky toy-like proportions, oversized head, short stubby legs, "
         "big hands, blocky chunky low-poly shapes with visible flat facets, simple surfaces, minimal small detail, readable silhouette, full body, standing in T-pose, facing forward. ")
TEX_STYLE = "Flat hand-painted look with soft color gradients, bright saturated colors, no photorealism, no text. "

def post(path, body):
    r = requests.post(API + path, headers=H, json=body, timeout=60)
    if r.status_code >= 300: raise SystemExit(f"POST {path} -> {r.status_code}: {r.text}")
    return r.json()["result"]

def wait(path, tid, label):
    while True:
        t = requests.get(f"{API}{path}/{tid}", headers=H, timeout=60).json()
        st = t.get("status"); print(f"[{label}] {st} {t.get('progress')}%", flush=True)
        if st == "SUCCEEDED": return t
        if st in ("FAILED", "CANCELED"): raise SystemExit(f"{label} fallo: {t.get('task_error')}")
        time.sleep(10)

def dl(url, path):
    with requests.get(url, stream=True, timeout=300) as r:
        r.raise_for_status()
        with open(path, "wb") as f:
            for c in r.iter_content(1 << 16): f.write(c)
    print("  descargado", os.path.basename(path), os.path.getsize(path) // 1024, "KB", flush=True)

def main(name):
    spec = json.load(open(os.path.join(ROOT, "characters.json")))[name]
    out = os.path.join(ROOT, "out", name); os.makedirs(out, exist_ok=True)
    sp = os.path.join(out, "state.json")
    s = json.load(open(sp)) if os.path.exists(sp) else {}
    save = lambda: json.dump(s, open(sp, "w"), indent=1)

    if "preview" not in s:
        s["preview"] = post("/v2/text-to-3d", {"mode": "preview", "ai_model": "meshy-6-lite", "prompt": (STYLE + spec["prompt"])[:800],
                         "pose_mode": "t-pose", "should_remesh": True, "topology": "triangle", "target_polycount": spec.get("polys", 12000)}); save()
    p = wait("/v2/text-to-3d", s["preview"], name + " forma")
    if "refine" not in s:
        s["refine"] = post("/v2/text-to-3d", {"mode": "refine", "preview_task_id": s["preview"], "ai_model": "meshy-6-lite",
                         "texture_prompt": (TEX_STYLE + spec["texture"])[:800], "texture_resolution": "2k"}); save()
    r = wait("/v2/text-to-3d", s["refine"], name + " textura")
    if not os.path.exists(os.path.join(out, "thumb.png")) and r.get("thumbnail_url"): dl(r["thumbnail_url"], os.path.join(out, "thumb.png"))
    if not os.path.exists(os.path.join(out, "textured.glb")): dl(r["model_urls"]["glb"], os.path.join(out, "textured.glb"))
    if "rig" not in s:
        s["rig"] = post("/v1/rigging", {"input_task_id": s["refine"], "height_meters": spec.get("height", 1.7)}); save()
    g = wait("/v1/rigging", s["rig"], name + " rig")
    json.dump(g, open(os.path.join(out, "rig_task.json"), "w"), indent=1)
    res = g.get("result") or {}
    def walk(o, pre=""):
        if isinstance(o, dict):
            for k, v in o.items(): yield from walk(v, pre + k + "_")
        elif isinstance(o, str) and o.startswith("http"): yield pre.rstrip("_"), o
    for k, url in walk(res):
        ext = ".fbx" if "fbx" in k else ".glb"
        fn = os.path.join(out, k.replace("_url", "") + ext)
        if not os.path.exists(fn): dl(url, fn)
    print("LISTO", name, "creditos rig:", g.get("consumed_credits"), flush=True)

if __name__ == "__main__": main(sys.argv[1])
