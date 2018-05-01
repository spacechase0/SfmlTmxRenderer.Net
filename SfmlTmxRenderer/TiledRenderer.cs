using SFML.Graphics;
using SFML.System;
using System;
using System.Collections.Generic;
using TiledSharp;

namespace SfmlTmxRenderer
{
    public class TiledRenderer
    {
        private class RenderLayer
        {
            private struct AnimIndex
            {
                public string whichTex;
                public int vertexOffset;
                public TmxLayerTile tile;
                public TmxTileset tileset;
                public TmxTilesetTile tsTile;
                public ICollection<TmxAnimationFrame> animInfo;
            }

            private TiledRenderer renderer;
            private TmxMap map;
            private TmxLayer layer;
            private IList<AnimIndex> anims = new List<AnimIndex>();
            private Dictionary<string, Vertex[]> vertices = new Dictionary<string, Vertex[]>();

            public RenderLayer( TiledRenderer renderer, string layer )
            {
                this.renderer = renderer;
                this.map = renderer.map;
                this.layer = map.Layers[layer];
                Build();
            }

            public void Build()
            {
                Dictionary<string, List<Vertex>> vertices = new Dictionary<string, List<Vertex>>();
                
                foreach ( var tile in layer.Tiles )
                {
                    if (tile.Gid == 0)
                        continue;

                    // Find the corresponding tileset
                    TmxTileset tileset = null;
                    foreach (var ts in map.Tilesets)
                    {
                        if (tile.Gid >= ts.FirstGid && tile.Gid < ts.FirstGid + ts.TileCount)
                        {
                            tileset = ts;
                            break;
                        }
                    }
                    if (tileset == null)
                        continue;

                    if (!vertices.ContainsKey(tileset.Image.Source))
                        vertices.Add(tileset.Image.Source, new List<Vertex>());
                    var vertexList = vertices[tileset.Image.Source];

                    // Prepare the vertices
                    Vertex tl = new Vertex(new Vector2f((float)(layer.OffsetX ?? 0) + tile.X * tileset.TileWidth, (float)(layer.OffsetY ?? 0) + tile.Y * tileset.TileHeight));
                    Vertex bl = new Vertex(new Vector2f(tl.Position.X, tl.Position.Y + tileset.TileHeight));
                    Vertex br = new Vertex(new Vector2f(tl.Position.X + tileset.TileWidth, tl.Position.Y + tileset.TileHeight));
                    Vertex tr = new Vertex(new Vector2f(tl.Position.X + tileset.TileWidth, tl.Position.Y));
                    
                    // Find the tile in the tileset
                    int lid = tile.Gid - tileset.FirstGid;
                    TmxTilesetTile tsTile = null;
                    foreach (var tsTileCheck in tileset.Tiles)
                    {
                        if (tsTileCheck.Id == lid)
                        {
                            tsTile = tsTileCheck;
                            break;
                        }
                    }

                    // Do animations
                    if (tsTile != null && tsTile.AnimationFrames != null && tsTile.AnimationFrames.Count != 0)
                    {
                        var anim = new AnimIndex();
                        anim.whichTex = tileset.Image.Source;
                        anim.vertexOffset = vertexList.Count;
                        anim.tile = tile;
                        anim.tileset = tileset;
                        anim.tsTile = tsTile;
                        anim.animInfo = tsTile.AnimationFrames;
                        anims.Add(anim);
                        lid = GetAnimationFrame(tsTile.AnimationFrames);
                    }
                    
                    // Do texture coordinates
                    UpdateTexCoords(tile, tileset, lid, ref tl, ref bl, ref br, ref tr);

                    // Add vertices to the layer for rendering
                    vertexList.Add(tl);
                    vertexList.Add(bl);
                    vertexList.Add(br);
                    vertexList.Add(tr);
                }

                this.vertices.Clear();
                foreach ( var vertexList in vertices )
                {
                    this.vertices.Add(vertexList.Key, vertexList.Value.ToArray());
                }
            }

            public void UpdateAnimations()
            {
                foreach ( var anim in anims )
                {
                    // Find the animation and change which tile we will use
                    int lid = anim.tile.Gid - anim.tileset.FirstGid;
                    if (anim.tsTile != null && anim.tsTile.AnimationFrames != null && anim.tsTile.AnimationFrames.Count != 0)
                    {
                        lid = GetAnimationFrame(anim.tsTile.AnimationFrames);
                    }

                    // Update the texture coordinates
                    ref Vertex tl = ref vertices[anim.whichTex][anim.vertexOffset + 0];
                    ref Vertex bl = ref vertices[anim.whichTex][anim.vertexOffset + 1];
                    ref Vertex br = ref vertices[anim.whichTex][anim.vertexOffset + 2];
                    ref Vertex tr = ref vertices[anim.whichTex][anim.vertexOffset + 3];
                    UpdateTexCoords(anim.tile, anim.tileset, lid, ref tl, ref bl, ref br, ref tr);
                }
            }

            public void Draw( RenderTarget target )
            {
                foreach ( var vertexArray in vertices )
                {
                    var tex = renderer.tilesheets[vertexArray.Key];
                    target.Draw(vertexArray.Value, PrimitiveType.Quads, new RenderStates(tex));
                }
            }

            private int GetAnimationFrame(ICollection<TmxAnimationFrame> frames)
            {
                int total = 0;
                foreach (var frame in frames)
                {
                    total += frame.Duration;
                }

                int curr = renderer.clock.ElapsedTime.AsMilliseconds() % total;
                foreach (var frame in frames)
                {
                    if (curr < frame.Duration)
                        return frame.Id;
                    curr -= frame.Duration;
                }

                return frames.GetEnumerator().Current.Id;
            }

            private void UpdateTexCoords( TmxLayerTile tile, TmxTileset tileset, int lid, ref Vertex tl, ref Vertex bl, ref Vertex br, ref Vertex tr )
            {
                // Columns is nullable? Okay...
                // Not sure why columns would be missing unless you're using an old version of Tiled?
                // Anyways, probably a better way to do this, but this is what I came up with
                // w = width, m = margin, c = columns, t = tile width, s = spacing
                // w = 2m + ct + s(c-1)
                // w = 2m + ct + cs - s
                // w = 2m + c(t + s) - s
                // w - 2m + s = c(t + s)
                // (w - 2m + s)/(t + s) = c
                // For example, with m=3, t=16, s=1, c=3 (which comes out to w=56)
                // (56 - 2(3) + 1)/(16 + 1) = 3
                // (56 - 6 + 1)/17 = 3
                // 51/17 = 3
                // 3 = 3
                int cols = tileset.Columns ?? (((int)renderer.tilesheets[tileset.Image.Source].Size.X - tileset.Margin * 2 + tileset.Spacing) / (tileset.TileWidth + tileset.Spacing));
                var texRect = new IntRect(tileset.Margin + lid % cols * (tileset.TileWidth + tileset.Spacing),
                                          tileset.Margin + lid / cols * (tileset.TileHeight + tileset.Spacing),
                                          tileset.TileWidth,
                                          tileset.TileHeight);

                // Probably a better to do this, too
                if (tile.DiagonalFlip)
                {
                    if (!tile.HorizontalFlip && !tile.VerticalFlip)
                    {
                        tl.TexCoords = new Vector2f(texRect.Left, texRect.Top);
                        bl.TexCoords = new Vector2f(texRect.Left + texRect.Width, texRect.Top);
                        br.TexCoords = new Vector2f(texRect.Left + texRect.Width, texRect.Top + texRect.Height);
                        tr.TexCoords = new Vector2f(texRect.Left, texRect.Top + texRect.Height);
                    }
                    else if (tile.HorizontalFlip && !tile.VerticalFlip)
                    {
                        tr.TexCoords = new Vector2f(texRect.Left, texRect.Top);
                        br.TexCoords = new Vector2f(texRect.Left + texRect.Width, texRect.Top);
                        bl.TexCoords = new Vector2f(texRect.Left + texRect.Width, texRect.Top + texRect.Height);
                        tl.TexCoords = new Vector2f(texRect.Left, texRect.Top + texRect.Height);
                    }
                    else if (!tile.HorizontalFlip && tile.VerticalFlip)
                    {
                        bl.TexCoords = new Vector2f(texRect.Left, texRect.Top);
                        tl.TexCoords = new Vector2f(texRect.Left + texRect.Width, texRect.Top);
                        tr.TexCoords = new Vector2f(texRect.Left + texRect.Width, texRect.Top + texRect.Height);
                        br.TexCoords = new Vector2f(texRect.Left, texRect.Top + texRect.Height);
                    }
                    else if (tile.HorizontalFlip && tile.VerticalFlip)
                    {
                        br.TexCoords = new Vector2f(texRect.Left, texRect.Top);
                        tr.TexCoords = new Vector2f(texRect.Left + texRect.Width, texRect.Top);
                        tl.TexCoords = new Vector2f(texRect.Left + texRect.Width, texRect.Top + texRect.Height);
                        bl.TexCoords = new Vector2f(texRect.Left, texRect.Top + texRect.Height);
                    }
                }
                else
                {
                    if (!tile.HorizontalFlip && !tile.VerticalFlip)
                    {
                        tl.TexCoords = new Vector2f(texRect.Left, texRect.Top);
                        bl.TexCoords = new Vector2f(texRect.Left, texRect.Top + texRect.Height);
                        br.TexCoords = new Vector2f(texRect.Left + texRect.Width, texRect.Top + texRect.Height);
                        tr.TexCoords = new Vector2f(texRect.Left + texRect.Width, texRect.Top);
                    }
                    else if (tile.HorizontalFlip && !tile.VerticalFlip)
                    {
                        tr.TexCoords = new Vector2f(texRect.Left, texRect.Top);
                        br.TexCoords = new Vector2f(texRect.Left, texRect.Top + texRect.Height);
                        bl.TexCoords = new Vector2f(texRect.Left + texRect.Width, texRect.Top + texRect.Height);
                        tl.TexCoords = new Vector2f(texRect.Left + texRect.Width, texRect.Top);
                    }
                    else if (!tile.HorizontalFlip && tile.VerticalFlip)
                    {
                        bl.TexCoords = new Vector2f(texRect.Left, texRect.Top);
                        tl.TexCoords = new Vector2f(texRect.Left, texRect.Top + texRect.Height);
                        tr.TexCoords = new Vector2f(texRect.Left + texRect.Width, texRect.Top + texRect.Height);
                        br.TexCoords = new Vector2f(texRect.Left + texRect.Width, texRect.Top);
                    }
                    else if (tile.HorizontalFlip && tile.VerticalFlip)
                    {
                        br.TexCoords = new Vector2f(texRect.Left, texRect.Top);
                        tr.TexCoords = new Vector2f(texRect.Left, texRect.Top + texRect.Height);
                        tl.TexCoords = new Vector2f(texRect.Left + texRect.Width, texRect.Top + texRect.Height);
                        bl.TexCoords = new Vector2f(texRect.Left + texRect.Width, texRect.Top);
                    }
                }
            }
        }

        internal TmxMap map;
        internal Clock clock = new Clock();
        internal Dictionary<string, Texture> tilesheets = new Dictionary<string, Texture>();
        private Dictionary<TmxLayer, RenderLayer> layers = new Dictionary<TmxLayer, RenderLayer>();
        public TiledRenderer(TmxMap map)
        {
            this.map = map;
            foreach ( var tileset in map.Tilesets )
            {
                tilesheets.Add(tileset.Image.Source, new Texture(tileset.Image.Source));
            }
        }

        public void UpdateAnimations()
        {
            foreach (var layer in map.Layers)
            {
                if (!layers.ContainsKey(layer))
                {
                    layers.Add(layer, new RenderLayer(this, layer.Name));
                    layers[layer].Build();
                }
                layers[layer].UpdateAnimations();
            }
        }

        public void DrawLayer( RenderTarget target, string layerName )
        {
            var layer = map.Layers[layerName];
            if (!layers.ContainsKey(layer))
            {
                layers.Add(layer, new RenderLayer(this, layer.Name));
                layers[layer].Build();
            }
            layers[layer].Draw(target);
        }

        public void Draw( RenderTarget target )
        {
            foreach ( var layer in map.Layers )
            {
                DrawLayer(target, layer.Name);
            }
        }
    }
}