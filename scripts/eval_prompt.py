"""
LLM Prompt Evaluator
Usage:
  python scripts/eval_prompt.py -f prompt.txt          # from file
  python scripts/eval_prompt.py -f prompts/*.txt       # batch compare
  python scripts/eval_prompt.py -p "You are 布洛克..."  # inline prompt
  python scripts/eval_prompt.py -f prompt.txt -n 3     # run 3 times
"""
import argparse, requests, time, re, sys, os
from pathlib import Path

API = "http://localhost:8000/api/v1/generate"

def llm_generate(prompt, max_tokens=1024, temperature=0.7, top_p=0.9, thinking=True):
    body = {"prompt": prompt, "max_tokens": max_tokens,
            "enable_thinking": thinking, "temperature": temperature, "top_p": top_p}
    t0 = time.time()
    r = requests.post(API, json=body, timeout=120)
    elapsed = (time.time() - t0) * 1000
    d = r.json()
    text = d["text"]
    think_m = re.search(r"<think>(.*?)</think>", text, re.DOTALL)
    think = think_m.group(1).strip() if think_m else ""
    output = text.replace(think_m.group(0), "").strip() if think_m else text.strip()
    return {"think": think, "output": output, "elapsed_ms": elapsed, "tokens": d["tokens_used"]}

def analyze(result, prompt):
    """Analyze the LLM response quality."""
    issues = []
    output = result["output"]
    think = result["think"]

    # JSON validity
    try:
        import json
        plan = json.loads(output).get("plan", [])
        if len(plan) == 0:
            issues.append("Plan empty — no actions generated")
        elif len(plan) == 1:
            issues.append(f"Only 1 step: {plan[0].get('action','?')}")
        else:
            actions = [s.get("action","?") for s in plan]
            issues.append(f"Plan: {len(plan)} steps — {actions}")
    except:
        issues.append(f"Invalid JSON: {output[:80]}")

    # Thinking quality
    if not think:
        issues.append("No thinking — model may be guessing")
    elif len(think) < 50:
        issues.append("Thinking too brief (< 50 chars)")
    elif len(think) > 2000:
        issues.append(f"Thinking too long ({len(think)} chars) — verbose reasoning")

    # Speed
    ms = result["elapsed_ms"]
    if ms < 1000: issues.append(f"Very fast ({ms:.0f}ms) — check if model loaded")
    elif ms > 30000: issues.append(f"Slow ({ms/1000:.1f}s) — consider reducing max_tokens")
    else: issues.append(f"Speed: {ms/1000:.1f}s")

    return issues

def print_result(prompt, result, label=""):
    header = f" {label} " if label else ""
    print("=" * 60)
    print(f"{header:=^60}")
    print("=" * 60)

    print("\n┌─ PROMPT " + "─" * 50)
    for line in prompt.strip().split("\n")[:30]:
        print(f"│ {line[:78]}")
    print("└" + "─" * 58)

    print(f"\n┌─ THINKING ({result['elapsed_ms']:.0f}ms, {result['tokens']} tokens) " + "─" * 30)
    think = result["think"]
    if think:
        for line in think.split("\n"):
            print(f"│ {line[:78]}")
    else:
        print("│ (none)")
    print("└" + "─" * 58)

    print(f"\n┌─ OUTPUT " + "─" * 51)
    for line in result["output"].split("\n"):
        print(f"│ {line[:78]}")
    print("└" + "─" * 58)

    print(f"\n┌─ ANALYSIS " + "─" * 48)
    for issue in analyze(result, prompt):
        print(f"│ {issue}")
    print("└" + "─" * 58)
    print()

def main():
    parser = argparse.ArgumentParser(description="LLM Prompt Evaluator")
    parser.add_argument("-f", "--file", help="Prompt file(s), supports glob")
    parser.add_argument("-p", "--prompt", help="Inline prompt text")
    parser.add_argument("-n", "--runs", type=int, default=1, help="Number of runs per prompt")
    parser.add_argument("-t", "--tokens", type=int, default=1024, help="max_tokens")
    parser.add_argument("--no-think", action="store_true", help="Disable thinking mode")
    args = parser.parse_args()

    prompts = []
    if args.file:
        import glob as g
        for pattern in args.file.split():
            for fp in g.glob(pattern):
                prompts.append((Path(fp).name, Path(fp).read_text(encoding="utf-8")))
    if args.prompt:
        prompts.append(("inline", args.prompt))

    if not prompts:
        print("Usage: eval_prompt.py -f prompt.txt | -p 'text'")
        sys.exit(1)

    for label, prompt in prompts:
        for run in range(args.runs):
            run_label = f"{label} (run {run+1}/{args.runs})" if args.runs > 1 else label
            result = llm_generate(prompt, max_tokens=args.tokens, thinking=not args.no_think)
            print_result(prompt, result, run_label)

if __name__ == "__main__":
    main()
