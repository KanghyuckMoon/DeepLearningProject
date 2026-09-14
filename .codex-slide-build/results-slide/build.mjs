import fs from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { Presentation, PresentationFile, FileBlob } from "@oai/artifact-tool";

const workspaceDir = "/home/user13/Documents/GitHub/DeepLearningProject";
const SKILL_DIR = "/home/user13/.codex/plugins/cache/openai-primary-runtime/presentations/26.904.11930/skills/presentations";
const RUNTIME_PYTHON = "/home/user13/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3";
const buildDir = path.join(workspaceDir, ".codex-slide-build/results-slide");
const stagingDir = path.join(workspaceDir, ".codex-finalizer/results-slide");
const outputDir = path.join(workspaceDir, "PresentationImg");
const finalPath = path.join(outputDir, "classification_results_slide.pptx");
const previewPath = path.join(outputDir, "classification_results_slide.png");

await fs.mkdir(buildDir, { recursive: true });
await fs.mkdir(stagingDir, { recursive: true });
await fs.mkdir(outputDir, { recursive: true });

const presentation = Presentation.create({
  slideSize: { width: 1280, height: 720 },
});
const slide = presentation.slides.add();
slide.background.fill = "#F7F9FC";
const font = "Noto Sans CJK KR";

const addText = (text, left, top, width, height, style = {}) => {
  const shape = slide.shapes.add({
    geometry: "textbox",
    position: { left, top, width, height },
    fill: "none",
    line: { fill: "none", width: 0 },
  });
  shape.text = text;
  shape.text.style = {
    typeface: font,
    fontSize: 24,
    color: "#17233C",
    autoFit: "none",
    verticalAlignment: "middle",
    ...style,
  };
  return shape;
};

// Slim accent and title establish the visual hierarchy without adding a cover.
slide.shapes.add({
  geometry: "rect",
  position: { left: 64, top: 54, width: 10, height: 78 },
  fill: "#18A7A0",
  line: { fill: "none", width: 0 },
});
addText("이미지 분류 결과 및 한계", 94, 46, 760, 58, {
  fontSize: 38,
  bold: true,
  color: "#12304A",
});
addText("정분류 예시는 확인했지만 실제 환경에서는 예측이 불안정했다", 96, 102, 900, 34, {
  fontSize: 19,
  color: "#516173",
});

// Native PowerPoint table: all cells remain editable.
const table = slide.tables.add({
  rows: 4,
  columns: 3,
  left: 64,
  top: 176,
  width: 744,
  height: 326,
  columnWidths: [265, 285, 194],
  values: [
    ["실제 물체", "AI 예측 결과", "판정"],
    ["니퍼", "니퍼", "일치"],
    ["와이어 스트리퍼", "와이어 스트리퍼", "일치"],
    ["펜", "펜", "일치"],
  ],
});
table.styleOptions = { headerRow: true, bandedRows: false };
table.rows[0].height = 68;
table.rows[1].height = 86;
table.rows[2].height = 86;
table.rows[3].height = 86;
table.borders.assign({ style: "solid", fill: "#D5DEE8", width: 1.25 });
table.cells.block({ row: 0, column: 0, rowCount: 1, columnCount: 3 }).assign({
  fill: "#173B59",
  textStyle: { typeface: font, fontSize: 21, bold: true, color: "#FFFFFF", alignment: "center", verticalAlignment: "middle" },
  margins: { left: 16, right: 16, top: 10, bottom: 10 },
  anchor: "middle",
});
table.cells.block({ row: 1, column: 0, rowCount: 3, columnCount: 3 }).assign({
  fill: "#FFFFFF",
  textStyle: { typeface: font, fontSize: 22, color: "#17233C", alignment: "center", verticalAlignment: "middle" },
  margins: { left: 16, right: 16, top: 10, bottom: 10 },
  anchor: "middle",
});
table.cells.block({ row: 1, column: 2, rowCount: 3, columnCount: 1 }).assign({
  fill: "#E7F7F4",
  textStyle: { typeface: font, fontSize: 21, bold: true, color: "#087B73", alignment: "center", verticalAlignment: "middle" },
});

// A single limitation field balances the evidence table and prevents overclaiming.
slide.shapes.add({
  geometry: "roundRect",
  position: { left: 850, top: 176, width: 366, height: 326 },
  fill: "#FFF3E8",
  line: { fill: "#F08A61", width: 2 },
});
addText("오분류 사례", 884, 202, 300, 48, {
  fontSize: 28,
  bold: true,
  color: "#B24D2A",
});
slide.shapes.add({
  geometry: "rect",
  position: { left: 884, top: 264, width: 54, height: 5 },
  fill: "#F08A61",
  line: { fill: "none", width: 0 },
});
addText("특정 각도, 배경 또는 조명 환경에서는 다른 물체로 잘못 분류되는 경우가 발생했다.", 884, 286, 296, 150, {
  fontSize: 23,
  color: "#5D3528",
  lineSpacing: 1.16,
});
addText("실제 사용 환경에서 추가 검증 필요", 884, 440, 296, 34, {
  fontSize: 18,
  bold: true,
  color: "#B24D2A",
});

// Interpretation note makes clear that the three rows are examples, not a metric.
slide.shapes.add({
  geometry: "rect",
  position: { left: 64, top: 548, width: 1152, height: 2 },
  fill: "#CBD5E1",
  line: { fill: "none", width: 0 },
});
addText("결과 해석", 64, 574, 160, 38, {
  fontSize: 21,
  bold: true,
  color: "#18A7A0",
});
addText("위 표는 정분류 예시이며 전체 인식률 또는 예측 정확도를 의미하지 않는다.", 218, 566, 965, 52, {
  fontSize: 23,
  bold: true,
  color: "#173B59",
});
addText("정확도를 제시하려면 클래스별 전체 테스트 건수와 정답·오답 수가 필요하다.", 218, 620, 965, 36, {
  fontSize: 18,
  color: "#667487",
});

slide.speakerNotes.textFrame.setText(
  "사용자가 제공한 정분류 예시와 오분류 조건만 반영했다. 정확도, 인식률, 신뢰도 수치는 제공되지 않아 표시하지 않았다."
);

const candidatePath = path.join(stagingDir, "candidate.pptx");
await (await PresentationFile.exportPptx(presentation)).save(candidatePath);

const { finalizePresentation } = await import(
  pathToFileURL(path.join(SKILL_DIR, "container_tools/artifact_tool_utils.mjs")).href,
);
await finalizePresentation({
  explicitTotalSlideCount: 1,
  workspaceDir,
  candidatePath,
  finalPath,
  pythonExecutable: RUNTIME_PYTHON,
  integrityValidatorPath: path.join(SKILL_DIR, "container_tools/inspect_presentation_package_integrity.py"),
  layoutValidatorPath: path.join(SKILL_DIR, "container_tools/inspect_presentation_layout_geometry.py"),
  layoutArgs: [
    "--expected-slide-size-emu", "12192000,6858000",
    "--validate-bullet-geometry",
    "--validate-heading-fit",
    "--require-native-table-slide", "1",
  ],
  requiredNativeTableOwnerSlides: [1],
  requiredNativeChartOwnerSlides: [],
  fontPolicy: { basis: "design", families: [font], scriptFonts: { ea: font } },
  verifyArtifactToolImport: true,
  receiptPath: path.join(stagingDir, "classification_results_slide.pptx.validation.json"),
});

const checked = await PresentationFile.importPptx(await FileBlob.load(finalPath));
const checkedSlide = checked.slides.getItem(0);
const preview = await checked.export({ slide: checkedSlide, format: "png", scale: 1.5 });
await fs.writeFile(previewPath, new Uint8Array(await preview.arrayBuffer()));

console.log(JSON.stringify({ finalPath, previewPath, candidatePath }, null, 2));
