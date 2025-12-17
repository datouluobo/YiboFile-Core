
import os

filepath = r"f:\Download\GitHub\OoiMRR\MainWindow.xaml.cs"
with open(filepath, 'r', encoding='utf-8') as f:
    lines = f.readlines()

regions = []
endregions = []

for i, line in enumerate(lines):
    if line.strip().startswith("#region"):
        regions.append(i+1)
        print(f"Line {i+1}: {line.strip()}")
    if line.strip().startswith("#endregion"):
        endregions.append(i+1)
        print(f"Line {i+1}: {line.strip()}")

print(f"Total #region: {len(regions)}")
print(f"Total #endregion: {len(endregions)}")
