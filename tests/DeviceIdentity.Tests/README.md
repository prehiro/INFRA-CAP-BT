# Device identity regressions

The console suite executes the real DynamicRecordService CreateAsync/UpdateAsync against a disposable SQLite in-memory database. It does not read application settings or connect to DEV/PROD SQL Server. Legacy duplicates are seeded directly only in that temporary database. The SQLite dependency belongs to this test project, not the API's production dependencies.

From the repository root (replace OUTSIDE_REPO with an absolute temporary directory):

    dotnet build tests/DeviceIdentity.Tests/DeviceIdentity.Tests.csproj --artifacts-path OUTSIDE_REPO
    dotnet OUTSIDE_REPO/bin/DeviceIdentity.Tests/debug/DeviceIdentity.Tests.dll

The separate artifacts directory avoids overwriting a running DEV API's binaries.

Coverage: owner-only edits retaining legacy duplicates; case/trim-equivalent unchanged identities; new or changed duplicate identities rejected; unique replacements accepted; deleted records excluded; No preserved and uniqueness still enforced; unrelated entities retain their original unique-field policy. Device rules cover pc_ledger.japan_hostname/computer_sn and factory_pc.sn even where old metadata does not mark them unique. Other fields remain metadata-driven.

Frontend tests:

    cd web
    node.exe --test tests/*.test.mjs

Browser QA must not save real records merely to test validation. Verify the record API fetch path is intercepted first; locally reject/capture mutating requests before transmission, exercise the form validation, restore all probes and re-read the real records. Treat this as UI validation coverage, not as a successful API save. Actual persistence is covered separately by the isolated CRUD tests above.
