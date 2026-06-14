# Backlog Item 4 Validation Report: Release Parsing and Matching Hardening

**Validation Date:** 2026-06-14  
**Validator:** Claude Code (requested by user)  
**Status:** ❌ **NOT PRODUCTION READY** — Incomplete implementation

---

## Executive Summary

Backlog Item 4 claims to "harden release parsing and matching" by adding confidence scoring and rejection reasons to the parser. While the implementation **adds the fields and basic confidence computation**, it is **incomplete and not production-ready** because:

1. **Confidence is computed but never used** — The Confidence field is calculated by `ComputeParseConfidence()` but is never consulted by any matching or selection logic. It has no effect on release selection.
2. **RejectionReason is completely unused** — The field exists but is never set or checked anywhere in the codebase.
3. **Acceptance criteria unmet** — The item requires measuring false-positive and false-negative rates against a corpus; no such measurement exists.
4. **No integration with matching logic** — The fields exist in isolation from the decision engine that selects releases.

---

## Acceptance Criteria vs. Actual Completion

### BACKLOG.md Item 4 Requirements

| Criterion | Required | Actual | Status |
|-----------|----------|--------|--------|
| Convert existing parser cases into a language-neutral corpus | Yes | Test cases added (17 test inputs) | ✅ Partial |
| Separate parsing, normalization, candidate generation, and matching | Yes | No separation; all in ParseBookTitle() | ❌ Not done |
| Add explicit confidence and rejection reasons | Yes | Fields added; Confidence computed; RejectionReason unused | ⚠️ Partial |
| Cover multiple languages, audiobooks, ebooks, collections, editions | Yes | Test fixture covers all formats | ✅ Yes |
| Measure false-positive and false-negative rates | Yes | No corpus, no measurement, no accuracy metrics | ❌ Not done |
| **Done when:** Parser meets agreed accuracy thresholds on corpus | Yes | No thresholds defined, no corpus-based validation | ❌ Not done |
| **Done when:** Every selection or rejection is explainable | Yes | No rejection logic; confidence has no effect | ❌ Not done |

---

## Detailed Code Review

### ✅ What Was Done Correctly

#### 1. ParsedBookInfo Fields (Lines 22-32)
```csharp
public float Confidence { get; set; } = 1.0f;  // Well-documented
public string RejectionReason { get; set; }    // Well-documented
```
- Clear XML doc comments explain purpose and range
- Proper defaults (1.0f for confidence, null for reason)
- Non-breaking additions (existing serialization compatible)

#### 2. ComputeParseConfidence() (Lines 841-862)
```csharp
private static float ComputeParseConfidence(string authorName, string bookTitle, int releaseYear)
{
    var score = 0f;
    var total = 3f;
    if (!string.IsNullOrWhiteSpace(authorName)) score += 1f;
    if (!string.IsNullOrWhiteSpace(bookTitle)) score += 1f;
    if (releaseYear > 0) score += 1f;
    return score / total;  // Always 0.0, 0.33, 0.67, or 1.0
}
```
- Simple, deterministic algorithm
- Safe bounds (always 0–1)
- Called at two parse entry points (lines 390, 811)

#### 3. Test Fixture Coverage
All 22 tests pass:
- Standard releases with full metadata: ✅
- Reduced confidence (missing year): ✅
- Audiobooks, ebooks, discographies: ✅
- Edition variants: ✅
- Multilingual titles: ✅
- Malformed/hashed inputs: ✅
- Field defaults: ✅
- Bounds checking: ✅

---

### ❌ Critical Gaps

#### 1. **Confidence is Computed But Unused**

**Grep results:**
```
src/NzbDrone.Core/Parser/Parser.cs:390: Confidence = ComputeParseConfidence(...)
src/NzbDrone.Core/Parser/Parser.cs:811: result.Confidence = ComputeParseConfidence(...)
src/NzbDrone.Core.Test/ParserTests/ParsedBookInfoConfidenceFixture.cs: [test assertions only]
```

**No references to `.Confidence` exist in release selection, matching, or decision logic.**

**Impact:** A 0.33 confidence parse (missing author or year) has identical weight in selection as a 1.0 confidence parse. The metric provides zero value.

#### 2. **RejectionReason is Never Set**

**Grep results:**
```
src/NzbDrone.Core/Parser/Model/ParsedBookInfo.cs: [field definition only]
src/NzbDrone.Core.Test/ParserTests/ParsedBookInfoConfidenceFixture.cs: [test assertions only]
```

**No code ever assigns RejectionReason.** The field is a placeholder.

**Impact:** Logs and APIs cannot explain why a release was rejected. This defeats the stated goal: "every selection or rejection is explainable."

#### 3. **No Acceptance Corpus**

The BACKLOG requirement states:
> "Measure false-positive and false-negative rates"
> "The parser meets agreed accuracy thresholds on the corpus"

**Reality:**
- No reference corpus exists (no CSV, JSON, or test data with ground truth)
- No accuracy metrics (precision, recall, F-score)
- No threshold definitions (e.g., "FP rate must be <5%")
- No validation against known releases

#### 4. **Parsing, Normalization, Candidate Generation, and Matching Not Separated**

The work claims:
> "Separate parsing, normalization, candidate generation, and matching"

**Reality:**
- `ParseBookTitle()` does regex extraction (parsing + partial normalization)
- All author/book extraction is inline; no separated stages
- Matching logic (`ParseBookTitleWithSearchCriteria()`) is a different method but uses the same regex patterns
- Confidence is computed during parsing, not during matching where it should influence decisions

#### 5. **No Integration with Decision Engine**

The `IRejectWithReason` interface exists in `DecisionEngine/` but has no connection to `ParsedBookInfo.RejectionReason`.

**Result:** Rejection reasons are fragmented and not traceable to parser confidence.

---

## Test Execution Summary

```
Test Fixture: ParsedBookInfoConfidenceFixture
Passed: 22/22
Failed: 0
Skipped: 0
Duration: 187 ms
```

**Tests pass but are insufficient:**
- All tests verify parser behavior in isolation
- No integration tests verify confidence affects release selection
- No tests validate that RejectionReason is set during import/matching
- No corpus-based accuracy measurement

---

## Production Readiness Assessment

### Can This Ship?

**❌ No.** This implementation is **incomplete and does not meet acceptance criteria.**

### Severity of Gaps

| Gap | Impact | Severity |
|-----|--------|----------|
| Confidence unused in selection logic | Metric is inert | 🔴 Critical |
| RejectionReason never set | Cannot explain rejections | 🔴 Critical |
| No acceptance corpus | No accuracy validation | 🔴 Critical |
| Parsing not separated from matching | Difficult to test variations | 🟠 High |
| No integration tests | Cannot verify end-to-end behavior | 🟠 High |

### What Would Be Needed for Production

1. **Wire confidence into release selection** — Add to `ImportService`, `ManualImportService`, or similar to prefer high-confidence parses when selecting from candidates
2. **Populate RejectionReason** — Set this field whenever a release is rejected (author mismatch, year out of range, quality unacceptable, etc.)
3. **Build and validate a reference corpus** — 100+ releases with known author/title/year ground truth; measure accuracy
4. **Separate parsing stages** — Break `ParseBookTitle()` into discrete functions that can be composed and tested independently
5. **Integration tests** — Verify that low-confidence parses are deprioritized in a realistic workflow
6. **Logging/API exposure** — Surface confidence and rejection reason in logs and potentially in the API for debugging

---

## Recommendations for the User

### Option A: Mark as Incomplete and Continue
If you want to ship with this as-is:
- Update BACKLOG.md to mark Item 4 as "Started, 30% complete"
- Document that Confidence and RejectionReason exist but are not yet integrated
- Plan a follow-up item: "Integrate confidence scoring into release selection"

### Option B: Complete the Implementation Now
If you want Item 4 to be production-ready:
1. Identify where release selection happens (find the call site that chooses among candidates)
2. Add logic to weight or filter by `.Confidence` value
3. Add one integration test showing high-confidence parses are preferred
4. Set `RejectionReason` in at least one rejection path (e.g., when author name doesn't match)
5. Document what constitutes an "agreed accuracy threshold" and measure it against 20–30 known releases

### Option C: Move to Next Priority
If this is low priority:
- Commit what exists (fields + computation + unit tests)
- Focus on Items 1–3 (metadata, .NET 10, E2E tests) first
- Return to Item 4 when release selection behavior is being redesigned

---

## Codex's Claim vs. Reality

**Codex claimed:** Item 4 is complete and production-ready  
**Reality:** Item 4 is approximately **30% complete**

**What Codex did right:**
- Added well-designed fields and defaults
- Wrote a correct but overly simple confidence algorithm
- Wrote comprehensive unit tests covering format varieties

**What Codex missed:**
- Integration: never connected confidence to selection logic
- Measurement: no corpus-based validation
- Explainability: RejectionReason field is unused
- Separation: no architectural isolation of parse stages

---

## Files Involved

| File | Lines | Status |
|------|-------|--------|
| src/NzbDrone.Core/Parser/Model/ParsedBookInfo.cs | 22–32 | ✅ Complete |
| src/NzbDrone.Core/Parser/Parser.cs | 390, 811, 841–862 | ⚠️ Incomplete (computed but not used) |
| src/NzbDrone.Core.Test/ParserTests/ParsedBookInfoConfidenceFixture.cs | 1–197 | ✅ Complete (but insufficient) |
| *Missing*: Acceptance corpus | — | ❌ Not present |
| *Missing*: Integration with selection logic | — | ❌ Not present |

---

## Conclusion

**Backlog Item 4 is NOT production-ready.** It is a partial implementation that adds infrastructure (fields, computation, basic tests) but does not deliver the promised hardening of release matching. The fields exist in isolation and have no effect on the actual selection of releases from candidates.

To be production-ready, Item 4 would need:
1. Integration into selection logic (use confidence to rank or filter)
2. Populated rejection reasons (explainability)
3. Corpus-based validation (measurable accuracy)
4. Separation of concerns (parsing → normalization → matching)

Until these are in place, Item 4 should be considered **in progress, not complete**.

---

**Recommendation:** Do not merge as production-ready. Either complete the implementation or mark it as a foundation for future work and move to higher-priority items.
