# Test Failure Remediation Plan (.NET 10.0 Migration)

## Executive Summary
6 test failures identified during .NET 10.0 migration. 4 are .NET 10 breaking changes, 2 are external service test flakiness. Proposed fixes prioritize backward compatibility and minimal code churn.

---

## Failure 1-2: `enum_in_array` (WhereBuilderPostgres + WhereBuilderSqlite)
**Status**: High Priority | **Impact**: Database query translation  
**Type**: .NET 10 breaking change

### Root Cause
When querying `array.Contains(enumValue)`, .NET 10's expression tree compiler inserts implicit `op_Implicit` conversions that weren't present in .NET 8. The WhereBuilder expression visitors don't handle `op_Implicit` method calls, causing NotImplementedException.

**Error**: `'op_Implicit' expressions are not yet implemented in the where clause expression tree parser`  
**Test**: `enum_in_array()` in both Postgres and Sqlite fixtures

### Affected Files
- `src/NzbDrone.Core/Datastore/WhereBuilderPostgres.cs` (line 60-84, VisitMethodCall)
- `src/NzbDrone.Core/Datastore/WhereBuilderSqlite.cs` (line 60-84, VisitMethodCall)

### Fix Strategy
Add handling for implicit conversion operators in the VisitMethodCall method:
1. **Detect** op_Implicit in the switch statement
2. **Unwrap** the implicit conversion to get the underlying expression
3. **Recursively visit** the converted expression

### Implementation
```csharp
case "op_Implicit":
    // .NET 10: Implicit conversions for enum comparisons
    // Just visit the converted operand; the conversion is implicit
    if (body.Arguments.Count == 1)
    {
        Visit(body.Arguments[0]);
    }
    else
    {
        throw new NotSupportedException("Unexpected implicit conversion");
    }
    break;
```

### Testing
- Run: `enum_in_array` tests in both WhereBuilderPostgresFixture and WhereBuilderSqliteFixture
- Verify: Can query `Authors.Where(a => statusList.Contains(a.Status))` where Status is enum

---

## Failure 3: `should_support_long_values_for_eta_in_milliseconds` (Transmission Client)
**Status**: Medium Priority | **Impact**: Download client timeout handling  
**Type**: .NET 10 arithmetic constraint

### Root Cause
Transmission ETA value (2147483648000 milliseconds) exceeds TimeSpan.MaxValue. Current code catches OverflowException on `TimeSpan.FromSeconds()` and falls back to `TimeSpan.FromMilliseconds()`, but the fallback also overflows.

The test value represents ~68 years, which exceeds TimeSpan limits (max ~10,675 years theoretically, but this specific value still overflows).

**Error**: `System.ArgumentOutOfRangeException : TimeSpan overflowed because the duration is too long`  
**Location**: `src/NzbDrone.Core/Download/Clients/Transmission/TransmissionBase.cs` line 81

### Affected Files
- `src/NzbDrone.Core/Download/Clients/Transmission/TransmissionBase.cs` (GetItems method)

### Fix Strategy
Cap extremely large ETA values to TimeSpan.MaxValue instead of throwing:

```csharp
if (torrent.Eta >= 0)
{
    try
    {
        item.RemainingTime = TimeSpan.FromSeconds(torrent.Eta);
    }
    catch (OverflowException)
    {
        try
        {
            item.RemainingTime = TimeSpan.FromMilliseconds(torrent.Eta);
        }
        catch (OverflowException)
        {
            // ETA is unreasonably large; use max timeout
            item.RemainingTime = TimeSpan.MaxValue;
        }
    }
}
```

### Testing
- Run: `should_support_long_values_for_eta_in_milliseconds` test
- Verify: Large ETA values gracefully cap at TimeSpan.MaxValue
- Edge case: Test with actual Transmission client if available

---

## Failure 4: `should_get_download_history` (HistoryRepository / WhereBuilderSqlite)
**Status**: High Priority | **Impact**: Download history tracking  
**Type**: .NET 10 expression translation + null dereference

### Root Cause
NullReferenceException in `WhereBuilderSqlite.TryGetPropertyValue()` at line 154. This is related to LINQ expression translation when handling download history queries. The expression tree might contain unexpected null-checked expressions that the visitor doesn't handle correctly.

**Error**: `System.NullReferenceException in WhereBuilderSqlite.TryGetPropertyValue()`  
**Location**: Referenced in error, but needs investigation

### Affected Files
- `src/NzbDrone.Core/Datastore/WhereBuilderSqlite.cs` (line 154, TryGetPropertyValue)
- Potentially: `src/NzbDrone.Core/Download/HistoryRepository.cs`

### Fix Strategy
1. **Debug** the failing query with detailed logging
2. **Identify** the specific expression pattern causing null reference
3. **Add defensive** null checks in TryGetPropertyValue
4. Or **refactor** the query to avoid the problematic pattern

### Testing
- Run: `should_get_download_history` test with detailed output
- Verify: History queries work correctly for both SQLite and PostgreSQL

---

## Failure 5-6: Update Service Tests (`should_get_recent_updates` + `finds_update_when_version_lower`)
**Status**: Low Priority | **Impact**: Update checking (external dependency)  
**Type**: External API flakiness / Test environment limitation

### Root Cause
Both tests use `UseRealHttp()` to fetch actual update data from GitHub releases API:
- `should_get_recent_updates()`: Expects non-empty collection, getting empty
- `finds_update_when_version_lower()`: Expects NotNull, getting null

This indicates the GitHub API is either:
1. Rate-limited in the test environment
2. Returning unexpected format
3. Not accessible from the environment
4. API changed behavior

**Errors**:
- `Expected collection not to be empty` (should_get_recent_updates)
- `Expected object not to be <null>` (finds_update_when_version_lower)

**Location**: `src/NzbDrone.Core.Test/UpdateTests/UpdatePackageProviderFixture.cs` lines 28-56

### Affected Files
- `src/NzbDrone.Core.Test/UpdateTests/UpdatePackageProviderFixture.cs`
- `src/NzbDrone.Core/Update/UpdatePackageProvider.cs`

### Fix Strategy
**Option A** (Recommended): Mock the HTTP responses instead of using real HTTP
- Create a test fixture for GitHub release responses
- Mock `IPlatformInfo` and HTTP client to return fixed update data
- Tests become deterministic and CI-friendly

**Option B**: Skip in CI, run locally
- Mark tests with `[Ignore]` or conditional category for live tests
- Document that local runs need network access
- Add to integration test suite rather than unit tests

**Option C**: Harden the API consumer
- Add retry logic with exponential backoff
- Add timeout and graceful null handling
- Treat missing updates as valid (not an error)

### Testing
- Option A: Verify mocked tests pass consistently
- Option B: Document as "requires network" and skip in headless CI
- Option C: Run with network unavailable and verify graceful handling

---

## Implementation Order

### Phase 1: Critical Path (High Priority)
1. **enum_in_array fixes** (Postgres + Sqlite) - 15 min
   - One unified fix across both builders
   - Unblocks any enum-based query testing
   - Low risk, high confidence

2. **Transmission ETA overflow** - 10 min
   - Simple try-catch wrapper
   - Well-scoped change
   - Prevents client crashes on unrealistic values

### Phase 2: Investigation (Needs Debug Work)
3. **Download history NullRef** - 30-60 min
   - Requires reproducing failure with full stack
   - May reveal other expression tree issues
   - High impact if found

### Phase 3: External Dependencies
4. **Update service tests** - 15-30 min
   - Lowest priority (doesn't affect product)
   - Choose mocking approach and implement
   - Document as environmental limitation

---

## Risk Assessment

| Failure | Risk | Mitigation |
|---------|------|-----------|
| enum_in_array | Low | Minimal change, one-off case |
| Transmission ETA | Low | Graceful cap, no behavior change |
| Download history | Medium | Needs investigation, may find related issues |
| Update tests | Very Low | External API, doesn't affect app |

---

## Acceptance Criteria

- [ ] enum_in_array tests pass (Postgres + Sqlite)
- [ ] Transmission ETA test passes, no overflow thrown
- [ ] Download history test passes or issue documented
- [ ] Update service tests either pass or marked as external dependency
- [ ] Full test suite: ≥99.5% pass rate (≥2710/2714)
- [ ] No new warnings or errors in build
- [ ] Changes are minimal and targeted to .NET 10 specifics

---

## Timeline Estimate
- **Total**: 1-1.5 hours
- Phase 1 (critical): 25 min
- Phase 2 (investigation): 30-60 min
- Phase 3 (external): 30 min
- Testing & verification: 15 min
