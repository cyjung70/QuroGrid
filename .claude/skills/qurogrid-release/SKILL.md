---
name: qurogrid-release
description: QuroGrid 패키지를 로컬 피드(/WRK/_nuget)로 배포해 QuroERP가 가져가게 할 때 사용. "QuroERP에 반영", "패키지 배포", "버전 올려" 요청에 쓴다.
---

# QuroGrid 로컬 배포 절차

배포 대상: `Quro.Grid.Data`, `Quro.Grid.Definition`, `Quro.Grid` (Sample·Tests 제외).

## 1. 사전 점검
- 작업 트리가 깨끗한지 (`git status`). 커밋되지 않은 변경이 있으면 멈추고 보고.
- `dotnet test` 전체 통과.
- 스킬 `qurogrid-review`에서 🔴 없음.

## 2. 버전 결정
- 현재 버전: `Directory.Build.props`의 `<VersionPrefix>`·`<VersionSuffix>`, 그리고 `ls /WRK/_nuget/quro.grid.*.nupkg`
- 규칙 (1.0 전):
  - 버그 수정·비파괴 추가 → 프리릴리스 번호만 증가: `0.3.0-alpha.1` → `0.3.0-alpha.2`
  - 깨지는 변경(CHANGELOG `BREAKING`) → MINOR 증가: `0.4.0-alpha.1`
- **이미 피드에 있는 버전 번호는 다시 쓰지 않는다.** (소비자 NuGet 캐시에 남아 반영되지 않는다)
- MINOR 증가는 **사용자 확인 후** 진행한다.

## 3. CHANGELOG
`CHANGELOG.md`의 `## Unreleased`를 `## <버전> — <YYYY-MM-DD>`로 바꾸고 새 `## Unreleased`를 위에 만든다.

## 4. 배포
```bash
pwsh scripts/pack-local.ps1 -Version <버전>
```
스크립트는 Release 빌드 → 3개 패키지 pack → `/WRK/_nuget`에 복사 → 결과 목록 출력을 한다.

## 5. 태그 · 보고
```bash
git add CHANGELOG.md Directory.Build.props
git commit -m "release: <버전>"
git tag v<버전>
```
## 6. nuget.org
`git push origin v<버전>`이 `.github/workflows/publish.yml`을 실행한다: 태그와 `Directory.Build.props` 버전 일치 확인 → 빌드·전체 테스트 → 다섯 패키지 pack → Trusted Publishing으로 nuget.org push.
- 결과 확인: `gh run list --workflow publish.yml`. 실패하면 고친 뒤 **새 버전**으로 다시 (같은 태그 재실행은 `--skip-duplicate`로 안전)
- 기존 태그 수동 게시: `gh workflow run publish.yml -f tag=v<버전>`
- nuget.org 버전은 지울 수 없다 (목록 숨김만 가능)

보고 내용 (QuroERP 쪽 작업자에게 전달):
- 새 버전, CHANGELOG 요약, `BREAKING` 여부와 소비자 수정 방법
- QuroERP에서 할 일: `build/QuroGrid.props`의 `QuroGridVersion`을 새 버전으로 (QuroERP 스킬 `quro-grid` 3절)

QuroERP 저장소 파일은 이 스킬에서 고치지 않는다.
