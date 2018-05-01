using SFML.Graphics;
using System;
using TiledSharp;

namespace SfmlTmxRenderer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var window = new RenderWindow(new SFML.Window.VideoMode(640, 480), "Window");

            var map = new TmxMap("map.tmx");
            var mapRenderer = new TiledRenderer(map);

            bool isRunning = true;
            window.Closed += (object sender, EventArgs evt) => isRunning = false;
            while ( isRunning )
            {
                window.DispatchEvents();

                window.Clear();
                mapRenderer.UpdateAnimations();
                mapRenderer.Draw( window );
                window.Display();
            }
        }
    }
}
