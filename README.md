# Introduction
Customize Trails through Daybreak font files (texture and fnt files), providing character replacement and Simplified/Traditional Chinese conversion functions. After conversion, replace the game files.
Tested on Clouded Leopard and Nisa versions, side effects unknown. Texture size used is Nisa version size: 4096*4096.

![main](Screenshots/main.png)

# Prerequisites
.net8

# Usage
### FNT Analysis
Taking Trails through Daybreak Nisa version as an example, open **ED9FontCreator** and drag **game\asset\common\font\font_*.fnt** to the Fnt file path text box, then click Analyze. If successful, it will display the total number of characters and data length.

### Font Settings
Set the font according to your needs. It is recommended to use a font with complete characters, such as the Source Han series fonts.
The default setting is Source Han Serif used by me personally.
After adjusting the settings, you can preview first. Note: Preview will use the character settings.

### Character Settings
Simplified/Traditional Chinese conversion and specific character replacement.

### Generation
After completing the above settings, proceed with the following operations:
1. Generate Character Images.
2. Export Font File.
3. Open the output directory, there should be 2 files inside, **font_*.fnt and font_*.dds**.
4. **Note: Backup!!** At this time, overwrite **font_*.fnt** to the **game\asset\common\font** directory,
overwrite the **font_*.dds** file to the **game\asset\dx11\image** directory, start the game to check if it is successful.

# Game Effect (Source Han Serif Bold)
![1](Screenshots/1.png)
![2](Screenshots/2.png)

# Reference
[Trails through Daybreak Toolkit (Updating: 2022/8/24)](https://bbs.3dmgame.com/forum.php?mod=viewthread&tid=6321673&page=1&extra=#pid301960392)
[ED9FontConverter](https://github.com/TwnKey/ED9FontConverter)
