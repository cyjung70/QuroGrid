---
name: qurogrid-review
description: QuroGrid 작업을 끝내기 전 자체 점검, 또는 PR·변경분 검토를 요청받았을 때 사용. R1~R7, Clean-room, 다국어, 공개 API, DataSet 규칙을 점검하고 🔴🟠⚪로 보고한다.
---

# QuroGrid 자체 점검

변경 파일 목록: `git diff --name-only origin/main...HEAD` (없으면 `git status`).

## 1. 자동 검사
```bash
dotnet build -warnaserror
dotnet test
# Clean-room 금지 식별자
git grep -nE "\b(setColumnEditable|setColumnSize|setImagePath|afterInit|edtxt)\b" -- src tests
# 하드코딩 UI 문자열 의심 (razor 안의 따옴표 한글·영문 문장)
git grep -nP '>[^<@{]*[가-힣A-Za-z]{3,}[^<]*<' -- "src/**/*.razor"
# 셀 단위 핸들러 의심
git grep -nE "@on(keydown|input|click)" -- "src/Quro.Grid/**/*.razor"
```

## 2. 체크리스트
| 항목 | 확인 |
|---|---|
| GS 테스트가 구현보다 먼저 있었고 통과 | 커밋 순서 또는 보고 |
| R1 JS로 넘기는 메타 최소 | quro-grid.js 호출 인자 |
| R2 키 입력마다 서버 호출 없음 | `@oninput`, `@onkeydown` |
| R3 붙여넣기·refs 호출 1회 | `AddRows` 사용 |
| R4 셀 컴포넌트 없음, `@key`·`ShouldRender` | razor |
| R5 셀 단위 핸들러 없음 | razor |
| R6 정의 Singleton 캐시, 페이징 | DI 등록 |
| R7 재연결 잠금·경고 | 컴포넌트 |
| Quro.Grid.Data에 UI 참조 없음 | csproj |
| 리소스 키 사용 | 위 grep |
| 새 의존성 라이선스 기록 | csproj diff |
| 공개 API 변경 → consumer-contract.md·CHANGELOG | public diff |
| QuroERP 업무 용어·URL 하드코딩 없음 | grep |

## 3. 보고 형식
- 🔴 차단: 반드시 고칠 것 (파일:줄, 규칙 ID)
- 🟠 권고
- ⚪ 정보
🔴가 하나라도 있으면 작업 완료로 보고하지 않는다.
