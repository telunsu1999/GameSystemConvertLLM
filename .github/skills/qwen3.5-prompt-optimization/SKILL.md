---
name: llm-prompt-optimizer
description: Analyze and optimize LLM prompts by observing real model responses, identifying issues, proposing targeted fixes, and validating improvements. Use when the user wants to improve prompt quality for any LLM-powered game system.
---

# LLM Prompt Optimizer

## When to use
- User asks to optimize prompts, improve LLM output quality, or fix parsing failures
- User notices LLM output is inconsistent, malformed, or not aligned with goals
- User wants to iterate on prompt design after observing real model behavior

## Workflow

### Phase 1: Observe — Understand the current prompt and LLM behavior
1. **Read the prompt construction code** — which file builds it, what data goes in, how is it structured
2. **Read the LLM call site** — what parameters (temperature, max_tokens, thinking), how is the response parsed
3. **Run the system and capture real LLM output** — use the traffic log (`logs/llm_server_traffic.log`) or run a test script
4. **Identify the gap** — what the prompt asks for vs what the model actually returns

### Phase 2: Diagnose — List optimization points before making any changes
For each issue found, document:
- **File + line** — where the code needs to change
- **Current behavior** — what's wrong now
- **Root cause** — why it happens (prompt structure? model limitation? parsing bug?)
- **Proposed fix** — concrete code change
- **Expected result** — what improvement to expect

> ⚠️ **Always list ALL optimizations FIRST, get user buy-in, then implement.** Never jump straight to coding.

### Phase 3: Implement — Make targeted changes
- Change one concern at a time where possible
- Prefer structural fixes (e.g., system/user split) over prompt text tweaks
- Update parsing code to match the model's actual output format

### Phase 4: Validate — Test with real LLM calls
1. Run the system and capture new LLM output
2. Compare before/after: output format, action correctness, token usage, latency
3. Check parsing success rate — did the parser extract the right actions?
4. If not fixed, go back to Phase 2 with new observations

---

## Common Diagnosis Patterns

### Prompt too long / mixed concerns
**Symptom:** Model ignores instructions, outputs minimal response
**Diagnosis:** All info in one text blob — model can't distinguish identity from state from task
**Fix:** Split into `system` (identity + output contract, ≤3 lines) and `user` (dynamic state, goals, context)

### Actions as text → fragile parsing
**Symptom:** JSON output is malformed, actions misnamed, params wrong
**Diagnosis:** Free-text action list forces model to copy-paste names; no type checking
**Fix:** Convert actions to OpenAI `tools` (function definitions with JSON Schema parameters)

### Model returns too few steps
**Symptom:** Only 1-2 actions when 4+ expected
**Diagnosis:** Example in prompt may constrain output; model may be too small for multi-step planning
**Fix:** Remove or expand example; ensure `max_tokens` is sufficient (≥1024 for multi-step)

### Thinking mode issues
**Symptom:** Model hangs, repeats itself, or output cut off mid-thought
**Diagnosis:** 
- 0.8B models: thinking mode causes infinite loops (official Qwen warning)
- 4B+ models: thinking is safe but adds latency and token cost
**Fix:** For 0.8B, disable thinking. For 4B+, evaluate if thinking improves output quality for the specific task.

### Parser can't extract plan
**Symptom:** `Plan parse failed` in logs
**Diagnosis:** Parser only handles one output format; model may output `<tool_call>` XML or `{"plan":[...]}` JSON
**Fix:** Support both formats — try `<function=NAME>` first, fallback to JSON regex

---

## Quick Reference: Qwen3.5 Model-Specific

| Model | thinking | tool_call format | best for |
|-------|----------|-----------------|----------|
| 0.8B | ❌ disabled | XML (unreliable) | prototyping only |
| 2B | ❌ prefer off | XML (improving) | testing |
| 4B | ✅ optional | XML (consistent) | production NPC planning |

Sampling defaults for text tasks:
- Non-thinking: `temperature=1.0, top_p=1.0`
- Thinking: `temperature=1.0, top_p=0.95`

---

## Output Format

After each Phase 2 (diagnosis), present findings as a table:

| # | File | Issue | Cause | Fix | Expected |
|---|------|-------|-------|-----|----------|
| 1 | `Foo.cs:42` | maxTokens=8 | typo/bug | change to 1024 | plans won't be truncated |
| 2 | `Bar.cs:88` | no system/user split | all text in one blob | split messages | role understanding ↑ |

After Phase 4 (validation), present before/after comparison.
