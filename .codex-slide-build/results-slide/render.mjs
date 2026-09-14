import fs from "node:fs/promises";
import { PresentationFile, FileBlob } from "@oai/artifact-tool";

const finalPath = "/home/user13/Documents/GitHub/DeepLearningProject/PresentationImg/classification_results_slide.pptx";
const previewPath = "/home/user13/Documents/GitHub/DeepLearningProject/PresentationImg/classification_results_slide.png";
const checked = await PresentationFile.importPptx(await FileBlob.load(finalPath));
const checkedSlide = checked.slides.getItem(0);
const preview = await checked.export({ slide: checkedSlide, format: "png", scale: 1.5 });
await fs.writeFile(previewPath, new Uint8Array(await preview.arrayBuffer()));
const layout = await checkedSlide.export({ format: "layout" });
await fs.writeFile(
  "/home/user13/Documents/GitHub/DeepLearningProject/.codex-finalizer/results-slide/final-slide.layout.json",
  await layout.text(),
);
console.log(previewPath);
