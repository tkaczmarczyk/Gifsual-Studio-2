
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;

namespace Gifsual_Studio_2
{
    internal sealed class RemoteImageAdornment
    {
        /// <summary>
        /// The layer of the adornment.
        /// </summary>
        private readonly IAdornmentLayer _layer;

        /// <summary>
        /// Text view where the adornment is created.
        /// </summary>
        private readonly IWpfTextView _view;

        /// <summary>
        /// Distance in pixels from the top edge of the image url to the image itself
        /// </summary>
        public const double MarginTop = 10;
        public const double MaxWidth = 500;
        public const double MaxHeight = 500;
        public const double IdleOpacity = 0.3;
        public const double FocusedOpacity = 0.6;

        private List<MediaElement> _renderedImages = new List<MediaElement>();
        private List<MediaElement> _currentFocusedImages = new List<MediaElement>();
        
        public RemoteImageAdornment(IWpfTextView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException("view");
            }

            _layer = view.GetAdornmentLayer("RemoteImageAdornment");
            
            _view = view;
            _view.LayoutChanged += OnLayoutChanged;

            _view.Caret.PositionChanged += Caret_PositionChanged;
        }

        private void Caret_PositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            if (_currentFocusedImages.Count > 0)
            {
                foreach (var image in _currentFocusedImages)
                    image.Opacity = IdleOpacity;

                _currentFocusedImages.Clear();
            }

            var lineUnderCaret = _view.Caret.ContainingTextViewLine;
            var lineContent = lineUnderCaret.Snapshot.GetText(lineUnderCaret.Start.Position, lineUnderCaret.Length);

            _renderedImages.ForEach(element => element.Opacity = IdleOpacity);

            //Only handle links.
            if (lineContent.Contains("http://"))
            {
                //Extract urls from line
                Regex linkParser = new Regex(@"\b(?:https?://|www\.)\S+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

                foreach (Match match in linkParser.Matches(lineContent))
                {
                    var url = match.Value;
                    foreach (var image in _renderedImages.Where(r => r.Source.AbsoluteUri == url))
                    {
                        image.Opacity = FocusedOpacity;
                        _currentFocusedImages.Add(image);
                    }
                }
            }
        }


        internal void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            foreach (ITextViewLine line in e.NewOrReformattedLines)
            {
                CreateVisuals(line);
            }
        }

        private void CreateVisuals(ITextViewLine line)
        {
            IWpfTextViewLineCollection textViewLines = _view.TextViewLines;
            var lineContent = line.Snapshot.GetText(line.Start.Position, line.Length);

            _renderedImages.RemoveAll(element => element.Parent == null);

            //Only handle links.
            if (lineContent.Contains("http://"))
            {
                //Extract urls from line
                Regex linkParser = new Regex(@"\b(?:https?://|www\.)\S+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

                foreach (Match match in linkParser.Matches(lineContent))
                {
                    var url = match.Value;
                    //Check if it's an image
                    var req = (HttpWebRequest)HttpWebRequest.Create(url);
                    req.Method = "HEAD";
                    try
                    {
                        using (var resp = req.GetResponse())
                        {
                            if (!resp.ContentType.ToLower(CultureInfo.InvariantCulture).StartsWith("image/"))
                            {
                                Debug.WriteLine($"{url} : Not an image");
                                continue;
                            }
                        }
                    }
                    catch (WebException ex)
                    {
                        //No internet or whatevs.
                        Debug.WriteLine($"{url} : Web exception");
                        continue;
                    }

                    //Add adornment
                    SnapshotSpan span = new SnapshotSpan(_view.TextSnapshot, Span.FromBounds(line.Start.Position + match.Index, line.Start.Position + match.Index + match.Length));
                    Geometry geometry = textViewLines.GetMarkerGeometry(span);

                    var image = new MediaElement
                    {
                        Source = new Uri(url, UriKind.Absolute),
                        Stretch = Stretch.Uniform,
                        Width = MaxWidth,
                        Height = MaxHeight,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        LoadedBehavior = MediaState.Play,
                        Opacity = IdleOpacity
                    };

                    image.MediaEnded += (sender, args) =>
                    {
                        ((MediaElement)sender).LoadedBehavior = MediaState.Manual;
                        ((MediaElement)sender).Position = new TimeSpan(0, 0, 1);
                        ((MediaElement)sender).Play();
                    };


                    // Clear the adornment layer of previous adornments
                    _layer.RemoveAdornmentsByVisualSpan(span);
                    
                    Canvas.SetLeft(image, geometry.Bounds.Left + geometry.Bounds.Width/2 - image.Width/2);
                    Canvas.SetTop(image, geometry.Bounds.Top + MarginTop);

                    // Add the image to the adornment layer and make it relative to the viewport
                    _layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, image, null);
                    _renderedImages.Add(image);

                    Debug.WriteLine($"{url} : Added adornment");
                }
            }
        }
    }
}
