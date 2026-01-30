---
name: serena
description: Reminder to use Serena MCP tools for code exploration and editing. Use when working with code symbols, navigating the codebase, or making edits. Serena provides semantic code tools that are more token-efficient than reading entire files.
disable-model-invocation: false
user-invocable: true
---

# Serena MCP Tools

You have a Serena MCP server running. Prefer Serena's semantic tools over reading entire files.

## Key Tools

- **`get_symbols_overview`** - Understand a file's structure without reading it entirely
- **`find_symbol`** - Search for classes, methods, fields by name pattern. Use `include_body: true` only when needed
- **`find_referencing_symbols`** - Find all references to a symbol across the codebase
- **`search_for_pattern`** - Regex search across files when you don't know the symbol name
- **`replace_symbol_body`** - Replace a symbol's definition precisely
- **`insert_before_symbol` / `insert_after_symbol`** - Add new code relative to existing symbols
- **`rename_symbol`** - Rename a symbol across the entire codebase
- **`think_about_task_adherence`** - Call before making edits to stay on track
- **`think_about_collected_information`** - Call after a sequence of searches to assess sufficiency

## Workflow

1. Use `get_symbols_overview` or `find_symbol` to explore, not `Read` on entire files
2. Only read symbol bodies (`include_body: true`) when you need to understand or edit them
3. Use `replace_symbol_body` for editing instead of line-based `Edit` when replacing whole methods/classes
4. Use `insert_after_symbol` / `insert_before_symbol` for adding new code
5. Call `think_about_task_adherence` before any code modification
6. Always `check_onboarding_performed` and `activate_project` at the start of a session if not already done
