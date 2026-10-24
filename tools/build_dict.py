#!/usr/bin/env python3
"""Собирает словари опечаток src/Core/Dict/{ru,en}.txt.gz.

Источники (см. dict/LICENSES.md):
  * Hunspell ru_RU (А. Лебедев, BSD-подобная) и en_US (SCOWL, MIT-подобная) —
    все словоформы разворачиваются через `unmunch` (пакет hunspell-tools);
  * wordfreq (Robyn Speer, данные CC BY-SA 4.0) — частоты и разговорные слова/сленг;
  * dict/curated/*.txt — термины и сленг (CC0).
Формат: строка `слово<TAB>частота`, частота = round(zipf*10) (0..80).
Запуск: sudo apt install hunspell-tools hunspell-ru hunspell-en-us; pip install wordfreq; python3 tools/build_dict.py
"""
import gzip, os, re, subprocess, sys
from wordfreq import get_frequency_dict, zipf_frequency

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HUN = os.environ.get("HUNSPELL_DIR", "/usr/share/hunspell")
LANGS = {"ru": ("ru_RU", re.compile(r"^[а-яё]+(?:-[а-яё]+)*$")),
         "en": ("en_US", re.compile(r"^[a-z]+(?:['-][a-z]+)*$"))}
MIN_ZIPF_EXTRA = 1.8   # слова только из wordfreq (сленг, редкие) — не ниже этой частоты

def unmunch(name):
    out = subprocess.run(["unmunch", f"{HUN}/{name}.dic", f"{HUN}/{name}.aff"], capture_output=True).stdout
    return out.decode("utf-8", "ignore").split("\n")

def deletes(w, n):
    res = {w}; cur = {w}
    for _ in range(n):
        cur = {x[:i] + x[i+1:] for x in cur for i in range(len(x))}
        res |= cur
    return res

def dl(a, b):
    """Расстояние Дамерау—Левенштейна (оптимальное выравнивание строк)."""
    d = [[0] * (len(b) + 1) for _ in range(len(a) + 1)]
    for i in range(len(a) + 1): d[i][0] = i
    for j in range(len(b) + 1): d[0][j] = j
    for i in range(1, len(a) + 1):
        for j in range(1, len(b) + 1):
            d[i][j] = min(d[i-1][j] + 1, d[i][j-1] + 1, d[i-1][j-1] + (a[i-1] != b[j-1]))
            if i > 1 and j > 1 and a[i-1] == b[j-2] and a[i-2] == b[j-1]: d[i][j] = min(d[i][j], d[i-2][j-2] + 1)
    return d[-1][-1]

for lang, (hname, rx) in LANGS.items():
    words = {}
    def add(w, f):
        w = w.strip().lower()
        if len(w) < 2 or not rx.match(w): return
        if f > words.get(w, -1): words[w] = f
    for w in unmunch(hname):
        w = w.split("/")[0].strip().lower()
        if rx.match(w or "-"): add(w, max(5, round(zipf_frequency(w, lang) * 10)))
    hun = set(words)
    extras = {}
    for w, p in get_frequency_dict(lang).items():
        z = zipf_frequency(w, lang)
        if w.lower() in hun: add(w, round(z * 10))
        elif z >= MIN_ZIPF_EXTRA and rx.match(w.lower()) and len(w) >= 2: extras[w.lower()] = z
    curated = set()
    for fn in os.listdir(f"{ROOT}/dict/curated"):
        if fn.startswith(lang + "_"):
            for line in open(f"{ROOT}/dict/curated/{fn}", encoding="utf-8"):
                if not line.startswith("#"): curated.update(x.lower() for x in line.split())
    # Отсеиваем частые опечатки из wordfreq («realy», «пошол»): слово выбрасывается, если рядом
    # (1 правка, для длинных — 2) есть словарное слово, которое минимум в ~6 раз частотнее.
    common = {w: f / 10 for w, f in words.items() if f >= 30}
    common.update({w: z for w, z in extras.items() if z >= 3.0 or w in curated})
    idx = {}
    for c in common:
        for d in deletes(c, 2): idx.setdefault(d, []).append(c)
    dropped = 0
    for w, z in extras.items():
        if w not in curated and len(w) >= 4:
            md = 1 if len(w) < 7 else 2
            cands = {c for d in deletes(w, md) for c in idx.get(d, ())}
            if any(c != w and common[c] >= z + 0.8 and dl(w, c) <= md for c in cands): dropped += 1; continue
        add(w, round(z * 10))
    print(lang, "dropped misspellings:", dropped, file=sys.stderr)
    for fn in os.listdir(f"{ROOT}/dict/curated"):
        if not fn.startswith(lang + "_"): continue
        for line in open(f"{ROOT}/dict/curated/{fn}", encoding="utf-8"):
            if line.startswith("#"): continue
            for w in line.split():
                add(w, max(30, round(zipf_frequency(w, lang) * 10)))
    if lang == "ru":  # «е» вместо «ё» — тоже правильное написание
        for w, f in list(words.items()):
            if "ё" in w: add(w.replace("ё", "е"), f)
    os.makedirs(f"{ROOT}/src/Core/Dict", exist_ok=True)
    items = sorted(words.items(), key=lambda kv: (-kv[1], kv[0]))
    with gzip.open(f"{ROOT}/src/Core/Dict/{lang}.txt.gz", "wt", encoding="utf-8", compresslevel=9) as f:
        for w, fr in items: f.write(f"{w}\t{fr}\n")
    print(lang, len(items), file=sys.stderr)
