# Changelog

QuroGrid 공개 API 변경 기록. 형식: 버전 — 날짜, 그 아래 Added / Changed / Fixed / `BREAKING:`.
소비자(QuroERP)는 버전을 올리기 전에 이 파일을 읽는다.

## Unreleased
- Changed: 화면 정의(TOON) 계약을 v0.2 스키마로 (문서만, Addendum A-44) — consumer-contract 4·6장. `Quro.Grid.Definition`·`<QuroGrid Definition>`은 구현 예정이라 아직 API 변화 없음
- Added: `Quro.Grid.Definition` 패키지 골격 (TOON 화면 정의 파서·검증기) — I-02 결정 대기

## 0.1.0-alpha.1 — 2026-10-29
첫 배포. `Quro.Grid.Data`, `Quro.Grid` (P0 + P1 전체). `Quro.Grid.Definition`은 아직 없음 (I-02)
- Added: 모든 공개 API에 XML 문서 주석 (IntelliSense)
- Added: `docs/integration/consumer-contract.md` 초안
- Added: 헤더 필터 (GS-071~GS-073, A-40) — `Filters`, `SetFilters`, `ClearFilters`, `OnFilterChanged`, `ColumnFilter`, `FilterChangedArgs`, `GridItemsRequest.Filters`, `GridExportRequest.Filters`, 리소스 키 `quro.filter.*`. 소비자 조치: 페이징 화면의 `ItemsProvider`는 `request.Filters`를 조회 조건에 반영해야 한다 (반영하지 않으면 필터가 무시된 결과가 보인다)
- Fixed: 페이징 모드에서 정렬·필터를 바꾼 뒤 행이 다시 그려지지 않던 문제 (PR #25)
- Added: 그룹핑·소계 (GS-074·GS-075, A-42) — `SetGroupBy`, `ClearGroupBy`, `GroupColumns`, `ExpandAll`, `CollapseAll`, 리소스 키 `quro.group.*`, CSS `.quro-group-head-row`·`.quro-subtotal-row`. 메모리 모드만
- Added: CSV 내보내기 요청 (GS-079, A-43) — `ExportFormat` (`Xlsx`·`Csv`, JSON `"xlsx"`·`"csv"`), `GridExportRequest.Format` (기본 `Xlsx`), `GetExportRequest(ExportFormat format = Xlsx)`. 기존 호출은 그대로 xlsx. 소비자 조치: 저장 API가 `format`을 읽어 CSV를 만든다
- Changed: 라이선스 MIT (`LICENSE.md`, Addendum A-41) — `Directory.Build.props`의 `PackageLicenseExpression`도 `MIT`
