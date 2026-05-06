---
description: Dummy-Human (Template) - Copy to create custom model agents
model: your-provider/your-model-name
mode: subagent
tools:
  write: true
  edit: true
  bash: true
  read: true
  glob: true
  grep: true
---

Faithfully executes instructions from Mask Weaver.

# Creating Custom Dummy-Humans

Copy this file to create agents for your desired models.

## Examples

### dummy-flash.md (Fast and cheap model)
```yaml
---
description: Dummy-Human (Flash) - Gemini Flash. Fast and cheap for simple tasks
model: google/gemini-2.5-flash
mode: subagent
---
```

### dummy-premium.md (Powerful reasoning model)
```yaml
---
description: Dummy-Human (Premium) - Claude Opus. For complex reasoning tasks
model: anthropic/claude-opus-4
mode: subagent
---
```

### dummy-deepseek.md (Coding specialized)
```yaml
---
description: Dummy-Human (DeepSeek) - DeepSeek Coder. Specialized for code generation
model: deepseek/deepseek-coder
mode: subagent
---
```

## Available Model Examples

| Model | Features | Use Case |
|-------|----------|----------|
| `google/gemini-2.5-flash` | Fast, cheap | Simple tasks, search |
| `anthropic/claude-sonnet-4` | Balanced | General coding |
| `anthropic/claude-opus-4` | Strong reasoning | Complex design |
| `openai/gpt-4o` | General purpose | Various tasks |
| `deepseek/deepseek-coder` | Coding specialized | Code generation |
