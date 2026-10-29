# Clean-room · 라이선스 규칙 (항상 적용)

참고 그리드에서는 **동작(무엇을 하는가)만** 가져오고, **코드(어떻게 구현했는가)는 가져오지 않는다.**

## 금지
- dhtmlxGrid 소스 코드의 복사, 포팅, REA 변환 결과의 반입 (GPL 계열 → MIT와 비호환)
- FarPoint Spread 바이너리 디컴파일·역공학 (상용 EULA)
- AG Grid **Enterprise** 기능 코드 참고·복사 (Community는 MIT, 개념 참고만 허용)
- Nexacro 런타임·라이브러리 코드 분석 (Dataset **개념**만 차용)
- 참고 그리드의 원본 소스를 이 저장소에 두는 것
- 참고 그리드의 형식·API 이름을 식별자로 쓰는 것: `ed`, `edtxt`, `txt`, `ch`, `setColumnEditable`, `setColumnSize`, `setImagePath`, `afterInit` 등
- 레거시 헤더 색(`#fff799` 등)으로 의미를 표현하는 것. 스타일은 편집 가능·필수 상태에서 계산한다

## 허용
- Procentro 레거시 XML(사용자 자산)에 적힌 **설정 값**(열 폭, 편집 가능 여부, 최대 길이, 정렬)을 데이터로 읽어 QuroGrid 고유 값으로 번역하는 것

## 의존성
- 추가 전 MIT 호환 확인, PR 설명에 라이선스 기록
- 허용 예: MudBlazor(MIT), Dapper(Apache 2.0), Npgsql(PostgreSQL), ClosedXML(MIT), xUnit·bUnit·Playwright
- GPL 계열 또는 상용 라이브러리는 추가하지 않는다

이 규칙에 어긋날 수 있는 요청을 받으면 작업을 멈추고 이유를 보고한다.
