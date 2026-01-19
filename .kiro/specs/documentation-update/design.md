# Design Document: Documentation Update

## Overview

This design outlines the approach for fixing and reorganizing the project documentation to accurately reflect the Simple EOS Matchmaking Service. The current documentation describes a completely different service (Guild Progress Service), which creates confusion for developers. This design ensures all documentation is accurate, well-organized, and maintainable.

## Architecture

### Documentation Structure

The documentation will follow a hierarchical structure:

```
/
├── README.md                    # Overview, quick start, high-level information
├── docs/
│   ├── architecture.md          # Technical architecture and design decisions
│   ├── setup.md                 # Detailed setup and configuration
│   ├── operations.md            # Testing, monitoring, troubleshooting
│   ├── testing_guide.md         # Comprehensive testing instructions
│   ├── devcontainer.md          # Dev container setup (existing, preserved)
│   └── images/                  # Screenshots and diagrams
│       ├── swagger-authorize.png
│       └── swagger-interface.png
```

### Navigation Pattern

Each documentation file will include:
- **Breadcrumb navigation** at the top linking to all other docs
- **Cross-references** to related sections in other files
- **"Next Steps"** section pointing to logical next documents

Example breadcrumb:
```markdown
**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md)
```

## Components and Interfaces

### Documentation Files

#### README.md (Root)
**Purpose:** Entry point providing overview and quick navigation

**Sections:**
- Project overview and features
- Quick start guide
- Project structure overview
- Basic configuration
- Links to detailed documentation
- Deployment overview

**Content Strategy:**
- Keep high-level and concise
- Link to detailed docs for deep dives
- Include visual diagrams (Mermaid)
- Provide quick commands for common tasks

#### docs/architecture.md
**Purpose:** Technical architecture and design decisions

**Sections:**
- Architecture overview
- Component descriptions (MatchmakingService, MatchPool, MatchMaker, SessionCreator, Notifier)
- Matching algorithm details
- Request lifecycle and state transitions
- EOS integration approach
- Extensibility points (INotifier interface)
- Design decisions and rationale

**Content Strategy:**
- Focus on "how it works" and "why"
- Include component interaction descriptions
- Explain design trade-offs
- Document extensibility patterns

#### docs/setup.md
**Purpose:** Complete setup and configuration guide

**Sections:**
- Prerequisites (tools, AGS account, EOS account)
- Environment configuration
- Configuration reference (MatchMaker, EOS, AccelByte)
- Building instructions
- Running locally
- Deployment to AGS

**Content Strategy:**
- Step-by-step instructions
- Complete configuration examples
- Troubleshooting common setup issues
- Platform-specific guidance (Windows/Linux/macOS)

#### docs/operations.md
**Purpose:** Day-to-day operations, testing, and monitoring

**Sections:**
- Local testing with Swagger UI
- Local testing with Postman
- Matchmaking flow examples
- Observability setup (Grafana, Loki, Prometheus)
- Error codes and handling
- Troubleshooting guide
- Development workflows

**Content Strategy:**
- Practical, actionable instructions
- Real examples and scenarios
- Common error solutions
- Monitoring and debugging tips

#### docs/testing_guide.md
**Purpose:** Comprehensive manual testing instructions

**Sections:**
- Prerequisites and setup
- Authentication setup
- Complete test flows
- Sample test data
- Expected responses
- Error scenario testing
- API response reference

**Content Strategy:**
- Complete end-to-end test scenarios
- Copy-paste ready examples
- Expected vs actual comparisons
- Troubleshooting test failures

## Data Models

### Documentation Content Sources

All documentation content must be derived from authoritative sources:

| Content Type | Source of Truth |
|--------------|----------------|
| API Endpoints | `Protos/matchmaking.proto` |
| Configuration | `appsettings.json`, `.env.template` |
| Components | Source code in `Services/`, `Model/`, `Classes/` |
| Permissions | Proto file permission annotations |
| Error Codes | gRPC status codes in service implementations |

### Content Validation Rules

1. **Endpoint Documentation**
   - Must match proto file exactly
   - Include all request/response fields
   - Document permission requirements

2. **Configuration Documentation**
   - Must match appsettings.json structure
   - Include all environment variables
   - Provide default values

3. **Code Examples**
   - Must be syntactically correct
   - Must work with actual service
   - Include realistic data

4. **Commands**
   - Must be tested and working
   - Include platform-specific variations
   - Show expected output

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Documentation Completeness

*For any* API endpoint defined in the proto file, there must exist corresponding documentation in either README.md or docs/ files that describes its purpose, parameters, and responses.

**Validates: Requirements 2.2, 5.1, 7.1**

### Property 2: Configuration Accuracy

*For any* configuration setting documented in setup.md, the setting name and structure must exactly match the corresponding field in appsettings.json or .env.template.

**Validates: Requirements 3.3, 3.4, 7.2**

### Property 3: Cross-Reference Integrity

*For any* documentation file in docs/, all internal links to other documentation files must resolve to existing files and sections.

**Validates: Requirements 6.6, 8.3**

### Property 4: Example Validity

*For any* code example or command in the documentation, the syntax must be valid for the specified language or shell, and the example must be consistent with the actual service implementation.

**Validates: Requirements 7.4, 7.5**

### Property 5: Terminology Consistency

*For any* technical term used in multiple documentation files, the term must have the same meaning and usage across all files.

**Validates: Requirements 8.4, 8.5**

## Error Handling

### Documentation Errors to Prevent

1. **Incorrect Service Description**
   - Verify all descriptions match matchmaking service, not guild service
   - Cross-check component names with source code

2. **Broken Links**
   - Validate all internal links before committing
   - Use relative paths for portability

3. **Outdated Examples**
   - Test all commands and examples
   - Update examples when code changes

4. **Configuration Mismatches**
   - Compare documented config with actual config files
   - Validate environment variable names

5. **Missing Prerequisites**
   - Document all required tools and accounts
   - Include version requirements

## Testing Strategy

### Documentation Validation

**Manual Review:**
- Read through all documentation as a new developer would
- Follow setup instructions on a clean environment
- Execute all example commands
- Test all API examples with actual service

**Automated Checks:**
- Markdown linting for syntax errors
- Link checking for broken references
- Spell checking for typos
- Code block syntax validation

**Cross-Reference Validation:**
- Compare endpoint docs with proto file
- Compare config docs with appsettings.json
- Compare component docs with source code
- Verify all examples work with actual service

### Test Scenarios

1. **New Developer Onboarding**
   - Can a new developer follow README to understand the project?
   - Can they follow setup.md to get service running?
   - Can they follow testing_guide.md to test endpoints?

2. **Configuration Changes**
   - If appsettings.json changes, does setup.md reflect it?
   - If .env.template changes, does setup.md reflect it?

3. **API Changes**
   - If proto file changes, do all docs reflect it?
   - If endpoints change, do examples still work?

4. **Deployment**
   - Can someone follow deployment instructions successfully?
   - Are all required secrets documented?

### Documentation Quality Metrics

- **Completeness**: All features documented
- **Accuracy**: Documentation matches implementation
- **Clarity**: Instructions are easy to follow
- **Maintainability**: Easy to update when code changes
- **Discoverability**: Easy to find relevant information

## Implementation Notes

### Content Migration Strategy

1. **Preserve Accurate Content**
   - Keep devcontainer.md (generic and accurate)
   - Keep images/ folder (screenshots still valid)
   - Extract accurate sections from root README

2. **Remove Incorrect Content**
   - Delete guild service documentation files
   - Remove any guild-specific examples
   - Remove incorrect architecture descriptions

3. **Create New Content**
   - Write new architecture.md for matchmaking
   - Write new setup.md for matchmaking
   - Write new operations.md for matchmaking
   - Write new testing_guide.md for matchmaking

4. **Reorganize Root README**
   - Keep overview and quick start
   - Move detailed setup to docs/setup.md
   - Move detailed testing to docs/testing_guide.md
   - Add navigation to detailed docs

### Writing Guidelines

1. **Use Active Voice**
   - "The service creates a match" not "A match is created"
   - "Configure the EOS settings" not "The EOS settings should be configured"

2. **Be Specific**
   - Use exact field names from code
   - Use exact command syntax
   - Provide concrete examples

3. **Structure for Scanning**
   - Use clear headings
   - Use bullet points for lists
   - Use code blocks for commands
   - Use tables for comparisons

4. **Provide Context**
   - Explain why, not just how
   - Link to related concepts
   - Note common pitfalls

### Maintenance Strategy

**When Code Changes:**
1. Update proto file → Update API documentation
2. Update appsettings.json → Update setup.md
3. Add new component → Update architecture.md
4. Change behavior → Update operations.md

**Regular Reviews:**
- Quarterly documentation review
- Test all examples still work
- Update screenshots if UI changes
- Check for broken links

**Version Control:**
- Document major changes in commit messages
- Tag documentation updates with version
- Keep changelog of doc updates
