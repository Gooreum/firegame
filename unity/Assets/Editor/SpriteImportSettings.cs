using UnityEditor;
using UnityEngine;

namespace FireGame.EditorTools
{
    /// <summary>
    /// Resources/Art 아래 PNG를 스프라이트로 임포트한다.
    ///
    /// 이 프로젝트는 2D 템플릿 없이 만들어져 PNG 기본 임포트 타입이 Sprite가 아니다.
    /// 그대로 두면 Resources.Load&lt;Sprite&gt;가 null을 돌려줘 화면이 텅 빈다.
    /// </summary>
    public sealed class SpriteImportSettings : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/Resources/Art/";

        /// <summary>1 월드 단위 = 타일 한 칸(64px). 격자 좌표를 그대로 월드 좌표로 쓸 수 있다.</summary>
        public const float PixelsPerUnit = 64f;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // 지도 배경은 화면 전체 크기라 최대 크기 제한에 걸리지 않게 한다.
            importer.maxTextureSize = 2048;
        }
    }
}
