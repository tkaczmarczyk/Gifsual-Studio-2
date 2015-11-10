//------------------------------------------------------------------------------
// <copyright file="TextAdornment1.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using System.Linq;
using System.Net;
using System.Windows.Threading;

namespace Gifsual_Studio_2
{
    /// <summary>
    /// TextAdornment1 places red boxes behind all the "a"s in the editor window
    /// </summary>
    internal sealed class RemoteImageAdornment
    {
        /// <summary>
        /// The layer of the adornment.
        /// </summary>
        private readonly IAdornmentLayer layer;

        /// <summary>
        /// Text view where the adornment is created.
        /// </summary>
        private readonly IWpfTextView view;

        public const double MarginTop = 10;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextAdornment1"/> class.
        /// </summary>
        /// <param name="view">Text view to create the adornment for</param>
        public RemoteImageAdornment(IWpfTextView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException("view");
            }

            this.layer = view.GetAdornmentLayer("RemoteImageAdornment");

            this.view = view;
            this.view.LayoutChanged += this.OnLayoutChanged;
        }

        /// <summary>
        /// Handles whenever the text displayed in the view changes by adding the adornment to any reformatted lines
        /// </summary>
        /// <remarks><para>This event is raised whenever the rendered text displayed in the <see cref="ITextView"/> changes.</para>
        /// <para>It is raised whenever the view does a layout (which happens when DisplayTextLineContainingBufferPosition is called or in response to text or classification changes).</para>
        /// <para>It is also raised whenever the view scrolls horizontally or when its size changes.</para>
        /// </remarks>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        internal void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            foreach (ITextViewLine line in e.NewOrReformattedLines)
            {
                this.CreateVisuals(line);
            }
        }

        /// <summary>
        /// Adds the scarlet box behind the 'a' characters within the given line
        /// </summary>
        /// <param name="line">Line to add the adornments</param>
        private void CreateVisuals(ITextViewLine line)
        {
            IWpfTextViewLineCollection textViewLines = this.view.TextViewLines;
            var lineContent = line.Snapshot.GetText(line.Start.Position, line.Length);
            
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
                    SnapshotSpan span = new SnapshotSpan(this.view.TextSnapshot, Span.FromBounds(line.Start.Position + match.Index, line.Start.Position + match.Index + match.Length));
                    Geometry geometry = textViewLines.GetMarkerGeometry(span);
                    
                    var image = new MediaElement()
                    {
                        Source = new Uri(url, UriKind.Absolute),
                        Stretch = Stretch.Uniform,
                        Width = 500,
                        Height = 500,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        LoadedBehavior = MediaState.Play,
                        Opacity = 0.5
                    };
                    image.MediaEnded += (sender, args) =>
                    {
                        
                        ((MediaElement)sender).LoadedBehavior = MediaState.Manual;
                        ((MediaElement)sender).Position = new TimeSpan(0, 0, 1);
                        ((MediaElement)sender).Play();
                    };


                    // Clear the adornment layer of previous adornments
                    this.layer.RemoveAdornmentsByVisualSpan(span);
                    
                    Canvas.SetLeft(image, geometry.Bounds.Left + geometry.Bounds.Width - image.Width/2);
                    Canvas.SetTop(image, geometry.Bounds.Top + MarginTop);

                    // Add the image to the adornment layer and make it relative to the viewport
                    this.layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, image, null);

                    Debug.WriteLine($"{url} : Added adornment");
                }
            }
        }
    }
}
