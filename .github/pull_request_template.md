<!-- PR title = squash commit message: type(scope): summary, e.g. feat(agora): add forum create page -->
## Summary
<!-- What does this PR do, in one or two sentences? -->

## Task
- Task ID: AA-
- Notion link:

## Type of change
- [ ] Feature
- [ ] Fix
- [ ] Refactor / cleanup
- [ ] Docs
- [ ] Infrastructure (Docker, config, tooling)

## System(s) touched
- [ ] Agora
- [ ] Argus
- [ ] Argus.Contracts ⚠️ shared: tell the other dev
- [ ] Argus.Client SDK ⚠️ shared: tell the other dev
- [ ] Docker / infrastructure

## What changed
<!-- Main changes: classes, pages, services, config -->
-

## Messages & database
- [ ] No message or database changes
- [ ] New or changed message type(s): <!-- name, direction, routing key -->
- [ ] New EF Core migration(s): <!-- migration name, which database -->

## How to test
<!-- Steps the other dev can follow to see it working -->
1.
2.

## Self-review checklist
- [ ] All the task's Acceptance Criteria are met (ticked in Notion)
- [ ] Builds with no errors; I ran it (normal path + at least one failure case)
- [ ] Messages look right in the RabbitMQ Management UI (if any are sent or received)
- [ ] No passwords, keys or connection strings committed (user-secrets / .env only)
- [ ] The other dev knows if Argus.Contracts or the SDK changed

## Notes for the other dev
<!-- Breaking changes, follow-ups, open questions. Delete if none. -->