﻿using Kokoro.Engine;
using Kokoro.Engine.Prefabs;
using Kokoro.Engine.Shaders;
using Kokoro.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TiledSharp;

namespace Akane.World
{
    public class Map
    {
        private TmxMap map;
        private Dictionary<string, Vector3> TileSets;   //X,Y = Offset in generated texture atlas, Z = texture atlas number
        private List<Texture> TextureAtlases;           //The Texture Atlases representing the textures on this map
        private List<Texture> HeightMaps;
        private List<Texture> TileLayers;               //The Tile Layers from bottom to top, each pixel on a texture stores information on the tile it represents
        private List<int> MaxGID_onLayer;
        private List<int> MinGID_onLayer;
        private List<Vector2> MinMaxTileset;
        private Vector2 TileSize;
        private Vector2 TilesCount;
        private FullScreenQuad quad;
        private ShaderProgram DefaultShader;
        private FrameBuffer tileMapPhaseA;
        private Vector2 viewportTileRes;
        private AkaneManager manager;

        public Vector2 MapSize;
        public Vector2 ViewportTileResolution
        {
            get
            {
                return viewportTileRes;
            }
            set
            {
                viewportTileRes = value;
                if (tileMapPhaseA != null)
                {
                    tileMapPhaseA["HeightMap"].Dispose();
                    tileMapPhaseA.Dispose();
                }
                tileMapPhaseA = new FrameBuffer((int)((value.X + 2) * TileSize.X), (int)((value.Y + 2) * TileSize.Y), PixelComponentType.RGBA8, manager.context);
                tileMapPhaseA.Add("HeightMap", new FrameBufferTexture((int)((value.X + 2) * TileSize.X), (int)((value.Y + 2) * TileSize.Y), PixelFormat.BGRA, PixelComponentType.RGBA8, PixelType.Float), FrameBufferAttachments.ColorAttachment1, manager.context);
            }
        }
        public Vector2 ViewportOffset;
        public Matrix4 ProjectionMatrix { get; private set; }
        public float AspectRatio { get { return viewportTileRes.X / viewportTileRes.Y; } }

        public Map(string path, AkaneManager akane)
        {
            map = new TmxMap(path);

            this.manager = akane;
            TileSets = new Dictionary<string, Vector3>();
            TextureAtlases = new List<Texture>();
            HeightMaps = new List<Texture>();
            TileLayers = new List<Texture>();
            MaxGID_onLayer = new List<int>();
            MinGID_onLayer = new List<int>();
            MinMaxTileset = new List<Vector2>();

            TileSize = new Vector2(map.TileWidth, map.TileHeight);
            TilesCount = new Vector2(map.Width, map.Height);
            MapSize = new Vector2(TileSize.X * TilesCount.X, TileSize.Y * TilesCount.Y);
            ViewportTileResolution = new Vector2(16, 9);

            #region TileLayers
            //Backup the projection matrix
            Matrix4 projBackup = akane.context.Projection;

            //Each pixel is one tile
            FrameBuffer renderTarget = new FrameBuffer(map.Width, map.Height, PixelComponentType.RGBA16f, akane.context);
            Model tileRenderer = new FullScreenQuad();
            tileRenderer.Materials[0].Shader = new Kokoro.Engine.Shaders.ShaderProgram(VertexShader.Load("Shaders/TileLayer"), FragmentShader.Load("Shaders/TileLayer"));

            Matrix4 TileLayersOrthoMatrix = Matrix4.CreateOrthographicOffCenter(0, map.Width, -map.Height, 0, -1.0f, 1.0f);
            akane.context.Projection = TileLayersOrthoMatrix;

            renderTarget.Bind(akane.context);

            for (int i = 0; i < map.Layers.Count; i++)
            {
                int maxGid = 0, minGid = 0;
                Vector2[,] tileInfo = GenerateTileIDArray(i, out maxGid, out minGid);

                akane.context.Clear(0, 0, 0, 0);

                //Generate a tile layer texture for this layer
                for (int x = 0; x < map.Width; x++)
                {
                    for (int y = 0; y < map.Height; y++)
                    {
                        //Need a shader to generate this
                        tileRenderer.Materials[0].Shader["Height"] = (float)(i) / (float)(map.Layers.Count);
                        tileRenderer.Materials[0].Shader["tileID"] = tileInfo[x, y].X;
                        tileRenderer.Materials[0].Shader["maxGID"] = (float)maxGid;
                        tileRenderer.World = Matrix4.CreateTranslation(x, -y, 0.0f);
                        tileRenderer.Draw(akane.context);
                    }
                }

                //Store the maxGid so we can reconstruct information from the tile later
                MaxGID_onLayer.Add(maxGid);
                MinGID_onLayer.Add(minGid);
                TileLayers.Add(renderTarget["Color"]);
                if (i < map.Layers.Count - 1) renderTarget.Add("Color", new FrameBufferTexture(map.Width, map.Height, PixelFormat.BGRA, PixelComponentType.RGBA16f, PixelType.Float), FrameBufferAttachments.ColorAttachment0, akane.context);    //Add a new texture to the framebuffer
            }

            //Restore the backed up matrix
            this.ProjectionMatrix = akane.context.Projection;
            akane.context.Projection = projBackup;
            #endregion

            quad = new FullScreenQuad();
            quad.Materials[0].Shader = new Kokoro.Engine.Shaders.ShaderProgram(VertexShader.Load("Shaders/LayerDrawer"), FragmentShader.Load("Shaders/LayerDrawer"));
            DefaultShader = new ShaderProgram(VertexShader.Load("Shaders/FrameBuffer"), FragmentShader.Load("Shaders/FrameBuffer"));

            LoadResources(akane);
        }

        private Vector2[,] GenerateTileIDArray(int layer, out int maxGid, out int minGid)
        {
            Vector2[,] ids = new Vector2[map.Width, map.Height];
            maxGid = 0;
            minGid = int.MaxValue;

            for (int index = 0; index < map.Layers[layer].Tiles.Count; index++)
            {
                TmxLayerTile t = map.Layers[layer].Tiles[index];
                ids[t.X, t.Y] = new Vector2(t.Gid, map.Tilesets.IndexOf(GetTileSetForTile(t.Gid)));
                if (t.Gid > maxGid) maxGid = t.Gid;
                if (t.Gid < minGid && t.Gid != 0) minGid = t.Gid;
            }

            return ids;
        }

        private TmxTileset GetTileSetForTile(int tileIndex)
        {
            var tiledTileset = map.Tilesets.Where(t => t.FirstGid <= tileIndex).OrderByDescending(t => t.FirstGid).FirstOrDefault();
            if (tiledTileset == null)
            {
                // Try to search the other way around. This should solve it...
                tiledTileset = map.Tilesets.Where(t => t.FirstGid >= tileIndex).OrderByDescending(t => t.FirstGid).FirstOrDefault();
                if (tiledTileset == null)
                {
                    // Still couldn't find the tileset.
                    string message = string.Format("Could not find tileset for tile #{0}.", tileIndex);
                    throw new InvalidOperationException(message);
                }
            }
            return tiledTileset;
        }

        private int CalculateFinalGID(int firstGID, int imageWidth, int imageHeight, int tileX, int tileY)
        {
            int nTilesX = imageWidth / tileX;
            int nTilesY = imageHeight / tileY;

            return firstGID + nTilesX * nTilesY;
        }


        //Load resources and apply their transparencies
        private void LoadResources(AkaneManager manager)
        {
            FrameBuffer tileSetTmpBuffer = new FrameBuffer((int)map.Tilesets[0].Image.Width, (int)map.Tilesets[0].Image.Height, PixelComponentType.RGBA8, manager.context);
            FullScreenQuad quad = new FullScreenQuad();
            quad.Materials[0].Shader = new Kokoro.Engine.Shaders.ShaderProgram(VertexShader.Load("Shaders/TransparentColor"), FragmentShader.Load("Shaders/TransparentColor"));


            for (int i = 0; i < map.Tilesets.Count; i++)
            {
                var t = map.Tilesets[i];

                if ((t.Properties.ContainsKey("Visible") && t.Properties["Visible"] != "false") || !t.Properties.ContainsKey("Visible"))
                {
                    tileSetTmpBuffer.Bind(manager.context);
                    MinMaxTileset.Add(new Vector2(t.FirstGid, CalculateFinalGID(t.FirstGid, (int)t.Image.Width, (int)t.Image.Height, t.TileWidth, t.TileHeight)));

                    manager.context.Clear(0, 0, 0, 0);
                    quad.Materials[0].ColorMap = new Texture(t.Image.Source);
                    quad.Materials[0].Shader["TransparentColor"] = new Vector3(t.Image.Trans.R / 255f, t.Image.Trans.G / 255f, t.Image.Trans.B / 255f);

                    quad.Draw(manager.context);

                    TextureAtlases.Add(tileSetTmpBuffer["Color"]);
                    if (i < map.Tilesets.Count - 1)
                        tileSetTmpBuffer.Add("Color",
new FrameBufferTexture((int)map.Tilesets[i + 1].Image.Width, (int)map.Tilesets[i + 1].Image.Height, PixelFormat.BGRA, PixelComponentType.RGBA8, PixelType.Float)
, FrameBufferAttachments.ColorAttachment0, manager.context);
                    //if (i < map.Tilesets.Count - 1) tileSetTmpBuffer = new FrameBuffer((int)map.Tilesets[i + 1].Image.Width, (int)map.Tilesets[i + 1].Image.Height, PixelComponentType.RGBA8, manager.context);

                    quad.Materials[0].ColorMap.Dispose();

                    if (t.Properties.ContainsKey("HeightMap"))
                    {
                        HeightMaps.Add(new Texture(t.Properties["HeightMap"]));
                    }
                    else
                    {
                        HeightMaps.Add(TextureAtlases[TextureAtlases.Count - 1]);
                    }

                }
            }
        }

        public void Draw(AkaneManager manager)
        {
            tileMapPhaseA.Bind(manager.context);
            for (int layer = 0; layer < TileLayers.Count; layer++)
            {
                int curMax = MinGID_onLayer[layer];
                for (int i = 0; i < MinMaxTileset.Count; i++)
                {
                    if (curMax < MinMaxTileset[i].Y)
                    {
                        quad.Materials[0].ColorMap = TextureAtlases[i];
                        quad.Materials[0].Shader["HeightMap"] = HeightMaps[i];
                        quad.Materials[0].Shader["firstGid"] = (float)MinMaxTileset[i].X;
                        quad.Materials[0].Shader["layerNum"] = (float)layer;
                        quad.Materials[0].Shader["maxLayer"] = (float)(TileLayers.Count - 1);
                        quad.Materials[0].Shader["TileIDs"] = TileLayers[layer];
                        quad.Materials[0].Shader["maxGid"] = (float)MaxGID_onLayer[layer];
                        quad.Materials[0].Shader["textureSize"] = TextureAtlases[i].Size;
                        quad.Materials[0].Shader["mapSize"] = TilesCount;
                        quad.Materials[0].Shader["tileSize"] = new Vector2(TileSize.X, TileSize.Y);
                        quad.Materials[0].Shader["viewportTileRes"] = new Vector2(ViewportTileResolution.X + 2, ViewportTileResolution.Y + 2);
                        quad.Materials[0].Shader["viewportOffset"] = new Vector2(ViewportOffset.X, ViewportOffset.Y + 1);

                        manager.context.Viewport = new Vector4(0, 0, manager.context.WindowSize.X, manager.context.WindowSize.Y);
                        quad.Draw(manager.context);
                    }

                }
            }
            tileMapPhaseA.UnBind();

            var tmp = quad.Materials[0].Shader;

            quad.Materials[0].Shader = DefaultShader;
            quad.Materials[0].ColorMap = tileMapPhaseA["Color"];
            quad.Draw(manager.context);
            quad.Materials[0].Shader = tmp;

            //TODO: The tilemap rendered has been adjusted to ensure that we always have the full amount of information available, next we draw it while adjusting it to have per pixel scrolling and control over scaling methods
            //Better scaling without any artifacts can be achieved by having the GPU sample the final render result

            //also, the heightfield should be downloaded to the CPU every other frame, downscaled and ray/path traced for lighting, this should be done in parallel
            //to the GPU work for best results

        }

        /*
         * TODO:bring over spritesheet class from older engine
         */

    }
}