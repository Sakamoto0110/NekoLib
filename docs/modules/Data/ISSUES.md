# NekoLib.Data Confirmed Issues

**Document ID:** DATA-ISSUES

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** confirmed defects in the NekoLib.Data boundary

**Surface:** issues

**Boundary:** data

**Authority role:** normative

**Mutation:** authored

**Indexing:** include

No confirmed Data defect remains at campaign baseline
`7796c20b304df26791b2d472a094ee92dc465b58`.

This registry does not absorb adjacent decision classes:

- **Promoted work:** `NEKOMKT-F026` in [`TODO.md`](../../../TODO.md) owns the
  accepted future removal of only the warning-obsolete
  `QueryBuilder.Join(string, string, string)` overload. Its release and
  implementation gates remain closed until the documented compatibility window
  and an explicit 2.0-or-later API authorization are satisfied. That planned
  removal is not a product defect.
- **Findings:** current unconfirmed observations belong in
  [`FINDINGS.md`](FINDINGS.md); none remain after this review.
- **Rejected alternatives:** the universal query family, implicit type
  guessing, mutation retry with another representation, and treating capability
  interfaces as provider plug-ins remain rejected in the historical audits.
- **Future provider ideas:** PostgreSQL, MySQL, ODBC, Oracle, MongoDB, or another
  provider require separately accepted support, translator, package-consumer,
  and runtime evidence. Their historical assessment does not promote them to
  `TODO.md` or make them supported.
