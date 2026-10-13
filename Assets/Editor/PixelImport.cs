using UnityEditor;
using UnityEngine;

namespace TinCan.EditorTools
{
    /// <summary>Assets/Art 아래 PNG는 전부 도트 규칙으로 들어온다 (PPU 36, Point, 압축 없음). 그림을 넣기만 하면 된다.</summary>
    public class PixelImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith("Assets/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 27;   // 화면 480×270 (사용자 10-13): 카메라 세로 10유닛 = 270px → 1그림픽셀 = 1080p에서 4픽셀
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.isReadable = true;   // 흰 실루엣(등장 때 흰색에서 제 색으로)을 코드로 만들 수 있게
            // 9슬라이스 자리(tip_panel 등)는 테두리 4px
            if (System.IO.Path.GetFileNameWithoutExtension(assetPath).EndsWith("_panel")) ti.spriteBorder = new Vector4(4, 4, 4, 4);
        }
    }
}
