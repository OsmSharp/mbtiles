using OsmSharp.Data.SQLite;
using OsmSharp.Logging;
using OsmSharp.Math.Geo;
using OsmSharp.Osm.Tiles;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace mbtiles
{
    class Program
    {
        static void Main(string[] args)
        {
            OsmSharp.Logging.Log.Enable();
            OsmSharp.Logging.Log.RegisterListener(new OsmSharp.WinForms.UI.Logging.ConsoleTraceListener());

            var outputDir = @"c:\temp\tiles";
            var mbtilesFile = outputDir + "\\tiles.mbtiles";
            SQLiteConnection.CreateFile(mbtilesFile);
            var mbtiles = new SQLiteConnection(string.Format("Data Source={0};Version=3;", mbtilesFile));
            mbtiles.Open();
            var query = new SQLiteCommand(mbtiles);
            query.CommandText = "CREATE TABLE metadata (name text, value text);";
            query.ExecuteNonQuery();
            new SQLiteCommand(mbtiles);
            query.CommandText = "CREATE TABLE tiles (zoom_level integer, tile_column integer, tile_row integer, tile_data blob);";
            query.ExecuteNonQuery();
            query = new SQLiteCommand(mbtiles);
            query.CommandText = "INSERT INTO metadata (name, value) VALUES ('name', 'tiles');" +
                "INSERT INTO metadata (name, value) VALUES ('type', 'baselayer');" +
                "INSERT INTO metadata (name, value) VALUES ('version', '1');" +
                "INSERT INTO metadata (name, value) VALUES ('minzoom', '5');" +
                "INSERT INTO metadata (name, value) VALUES ('maxzoom', '6');" +
                "INSERT INTO metadata (name, value) VALUES ('version', '1');" +
                "INSERT INTO metadata (name, value) VALUES ('description', 'A description of this layer');" +
                "INSERT INTO metadata (name, value) VALUES ('bounds', '-5.2294921875,42.195968776291780,8.50341796875,51.248163159055906');" + 
                "INSERT INTO metadata (name, value) VALUES ('format', 'png');";
            query.ExecuteNonQuery();
            query = new SQLiteCommand(mbtiles);
            query.CommandText = "INSERT INTO tiles VALUES (:zoom_level, :tile_column, :tile_row, :tile_data) ;";
            query.Parameters.Add(new SQLiteParameter(@"zoom_level", DbType.Int64));
            query.Parameters.Add(new SQLiteParameter(@"tile_column", DbType.Int64));
            query.Parameters.Add(new SQLiteParameter(@"tile_row", DbType.Int64));
            query.Parameters.Add(new SQLiteParameter(@"tile_data", DbType.Binary));

            //var url = "http://localhost:1234/default/{z}/{x}/{y}.png";
            var url = "https://a.tiles.mapbox.com/v4/mufort.d540a5b4/{z}/{x}/{y}.png?access_token=pk.eyJ1IjoibXVmb3J0IiwiYSI6ImNYZkRrQTAifQ.tJ1ZBrITTkFETMU8UvpWtQ";
            var box = new GeoCoordinateBox(
                new GeoCoordinate(42.195968776291780, -5.2294921875),
                new GeoCoordinate(51.248163159055906, 8.50341796875));
            var minZoom = 5;
            var maxZoom = 6;

            // download tiles.
            for (var zoom = maxZoom; zoom >= minZoom; zoom--)
            {
                var tileRange = TileRange.CreateAroundBoundingBox(box, zoom);
                OsmSharp.Logging.Log.TraceEvent(string.Empty, OsmSharp.Logging.TraceEventType.Information,
                    string.Format("Downloading {0} tiles at zoom {1}.",
                        tileRange.Count, zoom));
                foreach(var tile in tileRange)
                {
                    // download tile.
                    var data = Download(url, tile);

                    // save tile.
                    var tileDir = new DirectoryInfo(Path.Combine(outputDir, 
                        tile.Zoom.ToString(), tile.X.ToString()));
                    if (!tileDir.Exists)
                    { // creates target dir.
                        tileDir.Create();
                    }
                    var tileFile = new FileInfo(Path.Combine(tileDir.ToString(),
                        tile.Y.ToString() + ".png"));
                    using (var outputStream = tileFile.OpenWrite())
                    {
                        outputStream.Write(data, 0, data.Length);
                    }

                    var inverted = tile.InvertY();
                    
                    query.Parameters[0].Value = zoom;
                    query.Parameters[1].Value = tile.X;
                    query.Parameters[2].Value = inverted.Y;
                    query.Parameters[3].Value = data;
                    query.ExecuteNonQuery();

                    Thread.Sleep(100);
                }
            }

            mbtiles.Close();
        }

        private static byte[] Download(string url, Tile tile)
        { 
            // load the tile.
            url = url.Replace("{z}", tile.Zoom.ToString())
                    .Replace("{x}", tile.X.ToString())
                    .Replace("{y}", tile.Y.ToString());

            var request = (HttpWebRequest)HttpWebRequest.Create(
                              url);
            request.Accept = "text/html, image/png, image/jpeg, image/gif, */*";
            request.UserAgent = "OsmSharp/4.0";

            OsmSharp.Logging.Log.TraceEvent(string.Empty, TraceEventType.Information, "Request tile@" + url);

            return ReadToEnd(request.GetResponse().GetResponseStream());
        }

        public static byte[] ReadToEnd(System.IO.Stream stream)
        {
            long originalPosition = 0;

            if (stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            try
            {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;
                        }
                    }
                }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
                return buffer;
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = originalPosition;
                }
            }
        }
    }
}
