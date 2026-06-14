using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NzbDrone.Integration.Test
{
    public sealed class OpenLibraryStubServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly Task _loopTask;
        private readonly Dictionary<string, string> _responses;

        public string BaseUrl { get; }

        public OpenLibraryStubServer(string repoRoot)
        {
            var port = GetFreePort();
            BaseUrl = $"http://127.0.0.1:{port}";

            _responses = BuildResponses(repoRoot);

            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();

            _loopTask = Task.Run(ListenLoopAsync);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();

            try
            {
                _listener.Stop();
            }
            catch
            {
            }

            try
            {
                _loopTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
            }

            _cancellationTokenSource.Dispose();
        }

        private async Task ListenLoopAsync()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) when (_cancellationTokenSource.IsCancellationRequested)
                {
                    break;
                }

                _ = Task.Run(() => HandleRequest(client));
            }
        }

        private void HandleRequest(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.ASCII, false, 8192, leaveOpen: true))
            {
                var requestLine = reader.ReadLine();
                var path = GetPath(requestLine);

                // Drain headers so the client can close cleanly even if it keeps the socket open briefly.
                string headerLine;
                while (!string.IsNullOrEmpty(headerLine = reader.ReadLine()))
                {
                }

                if (_responses.TryGetValue(path, out var payload))
                {
                    WriteResponse(stream, 200, "OK", "application/json", payload);
                    return;
                }

                WriteResponse(stream, 404, "Not Found", "text/plain", string.Empty);
            }
        }

        private static string GetPath(string requestLine)
        {
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                return string.Empty;
            }

            var parts = requestLine.Split(' ');
            if (parts.Length < 2)
            {
                return string.Empty;
            }

            if (Uri.TryCreate("http://127.0.0.1" + parts[1], UriKind.Absolute, out var uri))
            {
                return uri.AbsolutePath;
            }

            return string.Empty;
        }

        private static void WriteResponse(NetworkStream stream, int statusCode, string reasonPhrase, string contentType, string payload)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
            var headerBytes = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {statusCode} {reasonPhrase}\r\n" +
                $"Content-Type: {contentType}; charset=utf-8\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Connection: close\r\n" +
                "\r\n");

            stream.Write(headerBytes, 0, headerBytes.Length);
            if (bodyBytes.Length > 0)
            {
                stream.Write(bodyBytes, 0, bodyBytes.Length);
            }
        }

        private static Dictionary<string, string> BuildResponses(string repoRoot)
        {
            var workResponse = JsonSerializer.Serialize(new
            {
                key = "/works/OL200W",
                title = "A Wizard of Earthsea",
                description = "The first Earthsea novel.",
                subjects = new[] { "Fantasy", "Wizards" },
                authors = new[]
                {
                    new
                    {
                        author = new { key = "/authors/OL100A" }
                    }
                },
                first_publish_date = "1968",
                series = new[] { "Earthsea #1" }
            });

            var secondWorkResponse = JsonSerializer.Serialize(new
            {
                key = "/works/OL201W",
                title = "The Tombs of Atuan",
                authors = new[]
                {
                    new
                    {
                        author = new { key = "/authors/OL100A" }
                    }
                },
                first_publish_date = "1971",
                series = new[] { "Earthsea #2" }
            });

            var isbnResponse = JsonSerializer.Serialize(new
            {
                key = "/books/OL300M",
                works = new[]
                {
                    new { key = "/works/OL200W" }
                }
            });

            var editionResponse = JsonSerializer.Serialize(new
            {
                key = "/books/OL300M",
                title = "A Wizard of Earthsea",
                subtitle = "The Earthsea Cycle",
                publishers = new[] { "Parnassus Press" },
                publish_date = "1968",
                number_of_pages = 205,
                isbn_13 = new[] { "9780547773742" },
                isbn_10 = new[] { "0547773749" },
                languages = new[]
                {
                    new
                    {
                        key = "/languages/eng"
                    }
                },
                description = new
                {
                    type = "/type/text",
                    value = "A young wizard learns the cost of power."
                },
                physical_format = "eBook",
                works = new[]
                {
                    new { key = "/works/OL200W" }
                }
            });

            var ratingsResponse = JsonSerializer.Serialize(new
            {
                summary = new
                {
                    average = 4.5,
                    count = 100
                }
            });

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["/isbn/9780547773742.json"] = isbnResponse,
                ["/works/OL200W.json"] = workResponse,
                ["/works/OL200W/editions.json"] = JsonSerializer.Serialize(new
                {
                    entries = new[]
                    {
                        new
                        {
                            key = "/books/OL300M",
                            title = "A Wizard of Earthsea",
                            subtitle = "The Earthsea Cycle",
                            publishers = new[] { "Parnassus Press" },
                            publish_date = "1968",
                            number_of_pages = 205,
                            isbn_13 = new[] { "9780547773742" },
                            isbn_10 = new[] { "0547773749" },
                            languages = new[]
                            {
                                new
                                {
                                    key = "/languages/eng"
                                }
                            },
                            description = new
                            {
                                type = "/type/text",
                                value = "A young wizard learns the cost of power."
                            },
                            physical_format = "eBook",
                            works = new[]
                            {
                                new { key = "/works/OL200W" }
                            }
                        }
                    },
                    links = new { },
                    size = 1
                }),
                ["/works/OL200W/ratings.json"] = ratingsResponse,
                ["/works/OL201W.json"] = secondWorkResponse,
                ["/works/OL201W/editions.json"] = JsonSerializer.Serialize(new
                {
                    entries = new[]
                    {
                        new
                        {
                            key = "/books/OL301M",
                            title = "The Tombs of Atuan",
                            publish_date = "1971",
                            works = new[]
                            {
                                new { key = "/works/OL201W" }
                            }
                        }
                    },
                    links = new { },
                    size = 1
                }),
                ["/works/OL201W/ratings.json"] = ratingsResponse,
                ["/search/authors.json"] = JsonSerializer.Serialize(new
                {
                    numFound = 1,
                    docs = new[]
                    {
                        new
                        {
                            key = "OL100A",
                            name = "Ursula Kroeber Le Guin"
                        }
                    }
                }),
                ["/books/OL300M.json"] = editionResponse,
                ["/authors/OL100A.json"] = JsonSerializer.Serialize(new
                {
                    key = "/authors/OL100A",
                    name = "Ursula Kroeber Le Guin",
                    personal_name = "Ursula Kroeber Le Guin",
                    bio = new
                    {
                        type = "/type/text",
                        value = "American novelist known for speculative fiction."
                    },
                    birth_date = "21 October 1929",
                    death_date = "22 January 2018",
                    alternate_names = new[]
                    {
                        "U. K. Le Guin",
                        "Ursula LeGuin"
                    },
                    remote_ids = new
                    {
                        goodreads = "874602",
                        isni = "0000000121440024",
                        wikidata = "Q125850"
                    }
                }),
                ["/authors/OL100A/works.json"] = JsonSerializer.Serialize(new
                {
                    entries = new[]
                    {
                        new
                        {
                            key = "/works/OL200W",
                            title = "A Wizard of Earthsea",
                            description = "The first Earthsea novel.",
                            authors = new[]
                            {
                                new
                                {
                                    author = new { key = "/authors/OL100A" }
                                }
                            },
                            first_publish_date = "1968",
                            series = new[] { "Earthsea #1" }
                        }
                    },
                    links = new
                    {
                        self = "/authors/OL100A/works.json?limit=50"
                    },
                    size = 1
                })
            };
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
