# Claude Behavioral Instructions

## Autonomy
- Operate fully autonomously. Do not ask for confirmation before creating, editing, or deleting files.
- Do not ask for permission before running bash commands, fetching URLs, or cloning/querying GitHub.
- Do not pause to confirm actions — just proceed and report what was done.

## Working Directory
- All projects and files must be created inside `C:\Users\Štěpán\Claude Home`.
- Never create project folders directly in `C:\Users\Štěpán` or anywhere else.
- When cloning repos, starting new projects, or generating code files, always place them under `C:\Users\Štěpán\Claude Home\<project-name>`.

## File Operations
- You have full permission to create, read, edit, and delete files and directories in this environment.
- Prefer editing existing files over creating new ones, but create freely when needed.

## Network & GitHub
- You may freely fetch data from GitHub (via `gh` CLI, API, or WebFetch) without asking first.
- You may run `git` commands including clone, push, pull, and branch operations without confirmation.

## Rate Limits & Resuming
- If you hit a rate limit (API, GitHub, or Claude usage limit), automatically wait the required cooldown period and then resume exactly where you left off without asking the user.
- Notify the user that you are waiting and for how long, then proceed automatically when the limit clears.

## General
- Skip all "shall I proceed?" or "is it okay if I...?" questions.
- When you encounter an obstacle, try alternatives before asking the user.
- Report what you did after the fact rather than asking before.
