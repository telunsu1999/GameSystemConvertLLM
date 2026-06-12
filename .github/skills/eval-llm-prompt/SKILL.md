# LLM Prompt Evaluator

## When to use
- User provides one or more prompt variants and wants to evaluate LLM output
- User asks "这个提示词效果怎么样" or "对比一下这两个提示词"
- User wants to iterate on prompt quality without running full game pipeline

## AI Workflow

1. User provides prompt content
2. Write it to a temp file: `scripts/_eval_temp.txt`
3. Run: `.\.venv\Scripts\python.exe scripts/eval_prompt.py -f scripts/_eval_temp.txt`
4. **先完整展示 LLM 思维链**，再逐项分析
5. Clean up: `Remove-Item scripts/_eval_temp.txt`

For comparing multiple variants, write each to a separate temp file and pass them all:
```
.\.venv\Scripts\python.exe scripts/eval_prompt.py -f scripts/_v1.txt scripts/_v2.txt
```

## Analysis Guidelines

After running, evaluate these aspects and tell the user:

### 1. Output Quality
- Is the JSON valid? Is the plan structure correct?
- Are the actions appropriate for the current time/context?
- Does the plan align with long-term goals? Did it pick up on goal hints?
- Is the plan diverse, or always eat→work?

### 2. Thinking Quality
- Does the thinking mention the goals/context provided?
- Is the reasoning coherent, or rambling/repetitive?
- Does the model identify the key decision factors?

### 3. Speed
- Too slow (>30s): consider reducing max_tokens or simplifying prompt
- Acceptable (10-25s): normal for thinking mode on this hardware
- Fast (<5s): thinking may be disabled or too brief

### 4. Consistency (when -n > 1)
- Does the model produce different plans, or always the same?
- Is the variability reasonable given the context?

### 5. Suggested Prompt Improvements
- Missing context that would help? Too verbose/too terse?
- Are goals clearly separated from daily tasks?
- Are available actions well-described?

## Prerequisites
- LLM server running on http://localhost:8000
- `requests` package installed in venv
3. Run `eval_prompt.py -f <file>` or `-p "<text>"`
4. Review THINKING, OUTPUT, and ANALYSIS sections
5. Iterate on prompt based on findings
