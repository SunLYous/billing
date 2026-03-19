#!/usr/bin/env python3
"""
Генератор тестовых данных + нагрузочный тест биллинга.

Использование:
  python generate_and_test.py                        # 10k звонков
  python generate_and_test.py --calls 500000         # 500k звонков
  python generate_and_test.py --generate-only        # только файлы
  python generate_and_test.py --concurrent 5         # 5 параллельных
  python generate_and_test.py --runs 3               # 3 прогона подряд
  python generate_and_test.py --no-verify-ssl        # для https://localhost

Требования: pip install requests
"""

import argparse
import csv
import io
import os
import random
import sys
import time
from datetime import datetime, timedelta
from pathlib import Path

# ─── Константы ────────────────────────────────────────────────

PREFIXES = [
    # prefix, destination, rate, conn_fee, timeband, weekday, priority
    ("7495", "Москва (городской)", "2.50", "0.00", "08:00-20:00", "1-5", "100"),
    ("7495", "Москва (городской, ночной)", "1.50", "0.00", "20:00-08:00", "1-5", "90"),
    ("7495", "Москва (городской, выходные)", "1.80", "0.00", "", "6-7", "80"),
    ("7812", "Санкт-Петербург", "2.30", "0.00", "08:00-20:00", "1-5", "100"),
    ("7812", "Санкт-Петербург (ночной)", "1.40", "0.00", "20:00-08:00", "1-5", "90"),
    ("7916", "Москва МТС (мобильный)", "1.80", "0.50", "", "", "50"),
    ("7926", "Москва Мегафон (мобильный)", "1.90", "0.50", "", "", "50"),
    ("7903", "Билайн", "1.75", "0.50", "", "", "50"),
    ("7911", "СПб МТС (мобильный)", "1.70", "0.50", "", "", "50"),
    ("7921", "СПб Мегафон (мобильный)", "1.85", "0.50", "", "", "50"),
    ("7831", "Нижний Новгород", "3.00", "1.00", "", "", "40"),
    ("7846", "Самара", "3.20", "1.00", "", "", "40"),
    ("7383", "Новосибирск", "3.50", "1.00", "", "", "40"),
    ("7861", "Краснодар", "3.10", "1.00", "", "", "40"),
    ("8800", "Бесплатный", "0.00", "0.00", "", "", "200"),
    ("7", "Россия (прочие)", "4.00", "1.50", "", "", "1"),
]

LAST_NAMES = [
    "Иванов", "Петров", "Сидоров", "Кузнецов", "Попов",
    "Смирнов", "Васильев", "Михайлов", "Фёдоров", "Новиков",
    "Козлов", "Морозов", "Волков", "Лебедев", "Соколов",
    "Зайцев", "Павлов", "Семёнов", "Голубев", "Виноградов",
]

INITIALS = [
    "И.И.", "П.П.", "А.С.", "В.М.", "О.Н.",
    "Д.А.", "Е.В.", "К.Р.", "Л.Г.", "М.Д.",
]

TRUNKS = ["SIP_Trunk_01", "SIP_Trunk_02", "SIP_Trunk_03", "E1_GW_01"]
ACCOUNTS = ["Office_Billing", "Sales_Dept", "Support", "IT_Dept", ""]

# Веса: 70% outgoing, 20% incoming, 10% internal
DIR_CHOICES = ["outgoing", "incoming", "internal"]
DIR_WEIGHTS = [0.70, 0.20, 0.10]

# Веса: 80% answered, 8% busy, 7% no_answer, 5% failed
DISP_CHOICES = ["answered", "busy", "no_answer", "failed"]
DISP_WEIGHTS = [0.80, 0.08, 0.07, 0.05]


# ─── Генерация данных ─────────────────────────────────────────

def generate_subscribers(count):
    subs = []
    used = set()
    for _ in range(count):
        while True:
            number = f"7812326{random.randint(1000, 9999)}"
            if number not in used:
                used.add(number)
                break
        name = f"{random.choice(LAST_NAMES)} {random.choice(INITIALS)}"
        subs.append((number, name))
    return subs


def generate_called_number():
    prefix_info = random.choice(PREFIXES)
    prefix = prefix_info[0]
    remaining = 11 - len(prefix)
    suffix = "".join(str(random.randint(0, 9)) for _ in range(remaining))
    return f"+{prefix}{suffix}"


def generate_cdr(subscribers, count, start_date):
    lines = []
    for i in range(count):
        caller = random.choice(subscribers)[0]
        called = generate_called_number()

        offset_sec = random.randint(0, 30 * 24 * 3600)
        start_time = start_date + timedelta(seconds=offset_sec)

        direction = random.choices(DIR_CHOICES, weights=DIR_WEIGHTS, k=1)[0]
        disposition = random.choices(DISP_CHOICES, weights=DISP_WEIGHTS, k=1)[0]

        if disposition == "answered":
            duration = random.randint(5, 1800)
            billable = max(0, duration - random.randint(0, 5))
        else:
            duration = random.randint(0, 30)
            billable = 0

        charge = "0.00"
        account = random.choice(ACCOUNTS)
        call_id = f"{i:012x}{random.randint(0, 0xFFFF):04x}"
        trunk = random.choice(TRUNKS)
        end_time = start_time + timedelta(seconds=duration)

        line = "|".join([
            start_time.strftime("%Y-%m-%d %H:%M:%S"),
            end_time.strftime("%Y-%m-%d %H:%M:%S"),
            caller,
            called,
            direction,
            disposition,
            str(duration),
            str(billable),
            charge,
            account,
            call_id,
            trunk,
        ])
        lines.append(line)
    return lines


def generate_tariff_csv():
    buf = io.StringIO()
    w = csv.writer(buf, delimiter=";")
    w.writerow(["prefix", "destination", "rate_per_min", "connection_fee",
                "timeband", "weekday", "priority", "effective_date", "expiry_date"])
    for p in PREFIXES:
        w.writerow([p[0], p[1], p[2], p[3], p[4], p[5], p[6],
                     "2026-01-01", "2026-12-31"])
    return buf.getvalue()


def generate_subscriber_csv(subscribers):
    buf = io.StringIO()
    w = csv.writer(buf, delimiter=";")
    w.writerow(["phone_number", "client_name"])
    for phone, name in subscribers:
        w.writerow([phone, name])
    return buf.getvalue()


def write_files(output_dir, cdr_lines, tariff_csv, subscriber_csv):
    output_dir.mkdir(parents=True, exist_ok=True)

    cdr_path = output_dir / "cdr.txt"
    with open(cdr_path, "w", encoding="utf-8") as f:
        f.write("\n".join(cdr_lines))

    tariff_path = output_dir / "tariffs.csv"
    with open(tariff_path, "w", encoding="utf-8") as f:
        f.write(tariff_csv)

    sub_path = output_dir / "subscribers.csv"
    with open(sub_path, "w", encoding="utf-8") as f:
        f.write(subscriber_csv)

    return cdr_path, tariff_path, sub_path


# ─── Загрузка и тест ──────────────────────────────────────────

def upload_test(url, cdr_path, tariff_path, sub_path, run_id=0, verify_ssl=True, mode="sync"):
    try:
        import requests
    except ImportError:
        print("  ОШИБКА: pip install requests")
        sys.exit(1)

    files = {
        "cdrFile": ("cdr.txt", open(cdr_path, "rb"), "text/plain"),
        "tariffFile": ("tariffs.csv", open(tariff_path, "rb"), "text/csv"),
        "subscriberFile": ("subscribers.csv", open(sub_path, "rb"), "text/csv"),
    }

    endpoint = f"{url}/api/billing/sync" if mode == "sync" else f"{url}/api/billing"
    print(f"  [{run_id:>3}] → {endpoint} ...", end=" ", flush=True)

    t0 = time.perf_counter()
    try:
        resp = requests.post(endpoint, files=files, timeout=600, verify=verify_ssl)
        elapsed = time.perf_counter() - t0

        ok = resp.status_code in (200, 202)
        tag = "✓" if ok else "✗"

        detail = ""
        if ok:
            try:
                data = resp.json()
                if mode == "sync":
                    server_ms = data.get("elapsedMs", "?")
                    batch_id = data.get("batchId", "?")
                    detail = f"batch={batch_id}  server={server_ms}ms"
                else:
                    batch_id = data.get("batchId", "?")
                    detail = f"batch={batch_id} (async)"
            except Exception:
                detail = resp.text[:100]
        else:
            detail = resp.text[:200]

        print(f"{tag}  HTTP {resp.status_code}  {elapsed:>8.3f}s  {detail}")

        return {
            "run": run_id, "status": resp.status_code,
            "elapsed": round(elapsed, 3), "detail": detail,
        }
    except Exception as e:
        elapsed = time.perf_counter() - t0
        print(f"✗  ERROR  {elapsed:>8.3f}s  {e}")
        return {"run": run_id, "status": 0, "elapsed": round(elapsed, 3), "error": str(e)}
    finally:
        for f in files.values():
            f[1].close()


def run_concurrent(url, cdr_path, tariff_path, sub_path, n, verify_ssl, mode="sync"):
    from concurrent.futures import ThreadPoolExecutor, as_completed

    results = []
    with ThreadPoolExecutor(max_workers=n) as pool:
        futs = {
            pool.submit(upload_test, url, cdr_path, tariff_path, sub_path, i, verify_ssl, mode): i
            for i in range(n)
        }
        for f in as_completed(futs):
            results.append(f.result())
    return sorted(results, key=lambda r: r["run"])


# ─── Отчёт ────────────────────────────────────────────────────

def print_report(results, call_count, sub_count):
    ok = [r["elapsed"] for r in results if r.get("status") in (200, 202)]
    fail = [r for r in results if r.get("status") not in (200, 202)]

    W = 60
    print(f"\n{'═' * W}")
    print(f"{'РЕЗУЛЬТАТЫ':^{W}}")
    print(f"{'═' * W}")
    print(f"  {'CDR записей:':<28} {call_count:>12,}")
    print(f"  {'Абонентов:':<28} {sub_count:>12,}")
    print(f"  {'Тарифов:':<28} {len(PREFIXES):>12,}")
    print(f"  {'Запросов всего:':<28} {len(results):>12,}")
    print(f"  {'Успешных:':<28} {len(ok):>12,}")
    print(f"  {'Ошибок:':<28} {len(fail):>12,}")

    if ok:
        avg = sum(ok) / len(ok)
        print(f"\n  {'Мин. время:':<28} {min(ok):>12.3f} сек")
        print(f"  {'Макс. время:':<28} {max(ok):>12.3f} сек")
        print(f"  {'Среднее время:':<28} {avg:>12.3f} сек")

        if len(ok) > 1:
            import statistics
            p50 = statistics.median(ok)
            p95 = sorted(ok)[int(len(ok) * 0.95)]
            print(f"  {'P50:':<28} {p50:>12.3f} сек")
            print(f"  {'P95:':<28} {p95:>12.3f} сек")

        print(f"\n  {'Пиковая (CDR/сек):':<28} {call_count / min(ok):>12,.0f}")
        print(f"  {'Средняя (CDR/сек):':<28} {call_count / avg:>12,.0f}")

    print(f"{'═' * W}")

    if fail:
        print("\n  Ошибки:")
        for e in fail:
            msg = e.get("error", f"HTTP {e.get('status')}")
            print(f"    [{e['run']:>3}] {msg}")
    print()


# ─── Main ─────────────────────────────────────────────────────

def main():
    ap = argparse.ArgumentParser(description="Нагрузочный тест биллинга")
    ap.add_argument("--calls", type=int, default=10_000, help="Кол-во CDR (default: 10000)")
    ap.add_argument("--subscribers", type=int, default=50, help="Кол-во абонентов (default: 50)")
    ap.add_argument("--url", default="http://localhost:8080", help="URL приложения")
    ap.add_argument("--output", default="./test_data", help="Папка для файлов")
    ap.add_argument("--generate-only", action="store_true", help="Только сгенерировать")
    ap.add_argument("--concurrent", type=int, default=1, help="Параллельных запросов")
    ap.add_argument("--runs", type=int, default=1, help="Последовательных прогонов")
    ap.add_argument("--no-verify-ssl", action="store_true", help="Не проверять SSL")
    ap.add_argument("--async", dest="async_mode", action="store_true",
                    help="Async: загрузить и не ждать (POST /api/billing)")
    args = ap.parse_args()

    verify_ssl = not args.no_verify_ssl
    mode = "async" if args.async_mode else "sync"

    # ── Генерация ──
    print(f"\n{'─' * 60}")
    print(f"  Генерация: {args.calls:,} CDR, {args.subscribers} абонентов, {len(PREFIXES)} тарифов")
    print(f"{'─' * 60}")

    t0 = time.perf_counter()
    subs = generate_subscribers(args.subscribers)
    cdr = generate_cdr(subs, args.calls, datetime(2026, 2, 1))
    tariff_csv = generate_tariff_csv()
    sub_csv = generate_subscriber_csv(subs)
    t_gen = time.perf_counter() - t0

    out = Path(args.output)
    cdr_path, tariff_path, sub_path = write_files(out, cdr, tariff_csv, sub_csv)

    mb = cdr_path.stat().st_size / 1024 / 1024
    print(f"  Готово за {t_gen:.2f}s → CDR {mb:.1f} MB, "
          f"тарифы {tariff_path.stat().st_size / 1024:.0f} KB, "
          f"абоненты {sub_path.stat().st_size / 1024:.0f} KB")

    if args.generate_only:
        print(f"  Файлы: {out.resolve()}\n")
        return

    # ── Disable SSL warnings ──
    if not verify_ssl:
        import urllib3
        urllib3.disable_warnings()

    # ── Тест ──
    url = args.url.rstrip("/")
    ep = "/api/billing" if mode == "async" else "/api/billing/sync"

    print(f"\n{'─' * 60}")
    print(f"  Тест → {url}{ep}")
    print(f"  Режим: {mode}, параллельность: {args.concurrent}, прогонов: {args.runs}")
    print(f"{'─' * 60}\n")

    all_results = []

    for run_idx in range(args.runs):
        if args.runs > 1:
            print(f"  ── Прогон {run_idx + 1}/{args.runs} ──")

        if args.concurrent > 1:
            res = run_concurrent(
                url, cdr_path, tariff_path, sub_path, args.concurrent, verify_ssl, mode)
        else:
            res = [upload_test(
                url, cdr_path, tariff_path, sub_path,
                run_idx, verify_ssl, mode)]

        all_results.extend(res)

    print_report(all_results, args.calls, args.subscribers)


if __name__ == "__main__":
    main()