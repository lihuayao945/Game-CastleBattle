using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;

/// <summary>
/// 精灵创建工具 - 用于在编辑器中生成简单的精灵图像
/// </summary>
public static class CreateSprites
{
    [MenuItem("Tools/Skills/Create Circle Sprite")]
    public static void CreateCircleSprite()
    {
        // 创建一个纹理
        int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        
        // 设置为透明
        Color[] colors = new Color[size * size];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.clear;
        }
        texture.SetPixels(colors);
        
        // 绘制圆形
        int radius = size / 2 - 4;
        int centerX = size / 2;
        int centerY = size / 2;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                
                // 绘制圆环
                if (distance < radius && distance > radius - 4)
                {
                    texture.SetPixel(x, y, Color.white);
                }
                // 绘制内部半透明区域
                else if (distance < radius - 4)
                {
                    texture.SetPixel(x, y, new Color(1, 1, 1, 0.2f));
                }
            }
        }
        
        texture.Apply();
        
        // 确保目录存在
        Directory.CreateDirectory("Assets/Skills/Sprites");
        
        // 保存为PNG
        byte[] bytes = texture.EncodeToPNG();
        string filePath = "Assets/Skills/Sprites/AuraCircle.png";
        File.WriteAllBytes(filePath, bytes);
        
        // 刷新资源
        AssetDatabase.Refresh();
        
        // 设置导入设置
        TextureImporter importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
        if (importer != null)
        {
            // 设置为Sprite
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            
            // 确保透明度正确
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            
            // 禁用mipmap，使用双线性过滤
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            
            // 禁用压缩，保持图像质量
            TextureImporterPlatformSettings platformSettings = new TextureImporterPlatformSettings();
            platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
            platformSettings.format = TextureImporterFormat.RGBA32;
            importer.SetPlatformTextureSettings(platformSettings);
            
            // 应用设置并重新导入
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            
            Debug.Log("圆形精灵导入设置已更新: " + filePath);
        }
        
        // 创建治疗特效精灵
        CreateHealEffectSprite();
    }
    
    [MenuItem("Tools/Skills/Create Rectangle Sprite")]
    public static void CreateRectangleSprite()
    {
        // 创建一个纹理
        int width = 256;
        int height = 256;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        
        // 设置为透明
        Color[] colors = new Color[width * height];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.clear;
        }
        texture.SetPixels(colors);
        
        // 绘制矩形边框
        int borderWidth = 4;
        int margin = 4;
        
        // 绘制外边框
        for (int y = margin; y < height - margin; y++)
        {
            for (int x = margin; x < width - margin; x++)
            {
                // 绘制边框
                if (x < margin + borderWidth || x >= width - margin - borderWidth ||
                    y < margin + borderWidth || y >= height - margin - borderWidth)
                {
                    texture.SetPixel(x, y, Color.white);
                }
                // 绘制内部半透明区域
                else
                {
                    texture.SetPixel(x, y, new Color(1, 1, 1, 0.2f));
                }
            }
        }
        
        texture.Apply();
        
        // 确保目录存在
        Directory.CreateDirectory("Assets/Skills/Sprites");
        
        // 保存为PNG
        byte[] bytes = texture.EncodeToPNG();
        string filePath = "Assets/Skills/Sprites/AuraRectangle.png";
        File.WriteAllBytes(filePath, bytes);
        
        // 刷新资源
        AssetDatabase.Refresh();
        
        // 设置导入设置
        TextureImporter importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
        if (importer != null)
        {
            // 设置为Sprite
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            
            // 确保透明度正确
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            
            // 禁用mipmap，使用双线性过滤
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            
            // 禁用压缩，保持图像质量
            TextureImporterPlatformSettings platformSettings = new TextureImporterPlatformSettings();
            platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
            platformSettings.format = TextureImporterFormat.RGBA32;
            importer.SetPlatformTextureSettings(platformSettings);
            
            // 应用设置并重新导入
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            
            Debug.Log("矩形精灵导入设置已更新: " + filePath);
        }
    }
    
    private static void CreateHealEffectSprite()
    {
        // 创建一个纹理
        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        
        // 设置为透明
        Color[] colors = new Color[size * size];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.clear;
        }
        texture.SetPixels(colors);
        
        // 绘制加号
        int thickness = 6;
        int length = size / 2;
        int centerX = size / 2;
        int centerY = size / 2;
        
        // 绘制水平线
        for (int y = centerY - thickness/2; y < centerY + thickness/2; y++)
        {
            for (int x = centerX - length/2; x < centerX + length/2; x++)
            {
                if (x >= 0 && x < size && y >= 0 && y < size)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }
        }
        
        // 绘制垂直线
        for (int x = centerX - thickness/2; x < centerX + thickness/2; x++)
        {
            for (int y = centerY - length/2; y < centerY + length/2; y++)
            {
                if (x >= 0 && x < size && y >= 0 && y < size)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }
        }
        
        texture.Apply();
        
        // 保存为PNG
        byte[] bytes = texture.EncodeToPNG();
        string filePath = "Assets/Skills/Sprites/HealEffect.png";
        File.WriteAllBytes(filePath, bytes);
        
        // 刷新资源
        AssetDatabase.Refresh();
        
        // 设置导入设置
        TextureImporter importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
        if (importer != null)
        {
            // 设置为Sprite
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            
            // 确保透明度正确
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            
            // 禁用mipmap，使用双线性过滤
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            
            // 禁用压缩，保持图像质量
            TextureImporterPlatformSettings platformSettings = new TextureImporterPlatformSettings();
            platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
            platformSettings.format = TextureImporterFormat.RGBA32;
            importer.SetPlatformTextureSettings(platformSettings);
            
            // 应用设置并重新导入
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            
            Debug.Log("治疗特效精灵导入设置已更新: " + filePath);
        }
    }
    
    [MenuItem("Tools/Skills/Create Default Projectile Sprite")]
    public static void CreateProjectileSprite()
    {
        // 创建一个纹理
        int width = 64;
        int height = 16;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        
        // 设置为透明
        Color[] colors = new Color[width * height];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.clear;
        }
        texture.SetPixels(colors);
        
        // 绘制简单的箭形投射物
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 绘制箭头主体
                if (x < width * 0.7f && y >= height/4 && y < height*3/4)
                {
                    texture.SetPixel(x, y, Color.white);
                }
                // 绘制箭头尖端
                else if (x >= width * 0.7f && 
                         y >= height/2 - (x - (int)(width * 0.7f)) && 
                         y <= height/2 + (x - (int)(width * 0.7f)))
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }
        }
        
        texture.Apply();
        
        // 确保目录存在
        Directory.CreateDirectory("Assets/Skills/Sprites");
        
        // 保存为PNG
        byte[] bytes = texture.EncodeToPNG();
        string filePath = "Assets/Skills/Sprites/ProjectileDefault.png";
        File.WriteAllBytes(filePath, bytes);
        
        // 刷新资源
        AssetDatabase.Refresh();
        
        // 设置导入设置
        TextureImporter importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
        if (importer != null)
        {
            // 设置为Sprite
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            
            // 确保透明度正确
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            
            // 禁用mipmap，使用双线性过滤
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            
            // 禁用压缩，保持图像质量
            TextureImporterPlatformSettings platformSettings = new TextureImporterPlatformSettings();
            platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
            platformSettings.format = TextureImporterFormat.RGBA32;
            importer.SetPlatformTextureSettings(platformSettings);
            
            // 应用设置并重新导入
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            
            Debug.Log("投射物精灵已创建: " + filePath);
        }
    }
}
#endif 