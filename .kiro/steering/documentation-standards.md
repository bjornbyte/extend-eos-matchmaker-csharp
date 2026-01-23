# Documentation Standards

## Documentation Structure

This project follows a structured documentation approach with clear separation of concerns:

### README.md - Project Overview
- **Purpose**: High-level overview and quick start guide
- **Audience**: New users, evaluators, quick reference
- **Content**:
  - Brief project description
  - Key features (bullet points)
  - Quick start instructions
  - Links to detailed documentation in /docs folder
  - Basic configuration examples
- **Style**: Concise, scannable, action-oriented
- **Length**: Keep under 500 lines; move detailed content to /docs

### /docs Folder - Detailed Documentation

All comprehensive documentation lives in the `/docs` folder with these standard files:

#### docs/setup.md - Setup and Configuration
- **Purpose**: Complete setup, configuration, and deployment instructions
- **Content**:
  - Prerequisites (tools, accounts, credentials)
  - Local development setup
  - Configuration options (all parameters explained)
  - Deployment instructions
  - Troubleshooting setup issues
- **Style**: Step-by-step, comprehensive, includes all options

#### docs/architecture.md - Technical Architecture
- **Purpose**: Technical design, architecture decisions, and extensibility
- **Content**:
  - Architecture overview with diagrams
  - Component descriptions and responsibilities
  - Data models and interfaces
  - Design patterns and decisions
  - Extensibility points with examples
  - Integration patterns
- **Style**: Technical, detailed, includes code examples and diagrams

#### docs/operations.md - Operations and Troubleshooting
- **Purpose**: Running, monitoring, and troubleshooting the service
- **Content**:
  - Testing procedures
  - Observability (metrics, tracing, logging)
  - Error codes and handling
  - Troubleshooting guides
  - Development workflows
- **Style**: Problem-solution format, practical, operational focus

#### docs/testing_guide.md - Testing Instructions
- **Purpose**: Comprehensive manual testing procedures
- **Content**:
  - Test prerequisites
  - Step-by-step test scenarios
  - Expected responses
  - Error scenario testing
  - Sample test data
- **Style**: Procedural, includes exact commands and expected outputs

## Documentation Organization Principles

### 1. Progressive Disclosure
- **README**: Brief overview with links
- **/docs**: Detailed information
- **Code comments**: Implementation details

Example:
```markdown
<!-- README.md -->
## Session Provider Modes

This service supports two modes: Create and Find.

> See [Architecture Guide](docs/architecture.md#session-provider-modes) for details.

<!-- docs/architecture.md -->
## Session Provider Modes

[Comprehensive explanation with diagrams, examples, code samples...]
```

### 2. Single Source of Truth
- Each concept documented in ONE primary location
- Other locations link to the primary documentation
- Avoid duplicating detailed explanations

### 3. Cross-Linking
- README links to /docs for details
- /docs files cross-link to related sections
- Use descriptive link text and anchor links

Example:
```markdown
> See [Architecture Guide](docs/architecture.md#session-provider-modes) for detailed information on both modes, use cases, and extensibility examples.
```

### 4. Consistent Navigation
Every /docs file should include navigation header:
```markdown
**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md)
```

## Writing Style Guidelines

- **Be concise**: Remove unnecessary words
- **Be specific**: Use concrete examples over abstract descriptions
- **Be actionable**: Tell users what to do, not just what exists
- **Be consistent**: Use same terms throughout documentation
- **Code examples**: Include complete, working examples with registration/setup code
- **Configuration**: Show both JSON and environment variable formats with defaults
- **Diagrams**: Use Mermaid for flows, tables for comparisons

## Documentation for New Features

Update documentation in this order:
1. **docs/architecture.md** - Detailed technical explanation, diagrams, code examples
2. **docs/setup.md** - Configuration options, environment variables
3. **README.md** - Brief overview (2-3 sentences) with link to architecture guide
4. **docs/operations.md** - Troubleshooting if needed
5. **docs/testing_guide.md** - Test scenarios if user-facing

## Quick Reference

**Anti-Patterns:**
- ❌ Duplicate detailed explanations across files
- ❌ Put comprehensive docs in README
- ❌ Write documentation without examples

**Best Practices:**
- ✅ Link to primary documentation source
- ✅ Keep README concise with links to /docs
- ✅ Include working code examples with registration
