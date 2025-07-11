﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using QuestPDF.Companion;
using QuestPDF.Drawing.DrawingCanvases;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Skia;

namespace QuestPDF.Drawing.DocumentCanvases
{
    internal sealed class CompanionPageSnapshot
    {
        public SkPicture Picture { get; set; }
        public Size Size { get; set; }

        public CompanionPageSnapshot(SkPicture picture, Size size)
        {
            Picture = picture;
            Size = size;
        }
        
        public byte[] RenderImage(int zoomLevel)
        {
            // prepare canvas
            var scale = (float)Math.Pow(2, zoomLevel);
            
            using var bitmap = new SkBitmap((int)(Size.Width * scale), (int)(Size.Height * scale));
            using var canvas = SkCanvas.CreateFromBitmap(bitmap);
            canvas.Scale(scale, scale);
            
            // draw white background
            using var backgroundPaint = new SkPaint();
            backgroundPaint.SetSolidColor(Colors.White);
            
            var backgroundRect = new SkRect(0, 0, Size.Width, Size.Height);
            canvas.DrawRectangle(backgroundRect, backgroundPaint);
            
            // draw content
            canvas.DrawPicture(Picture);
            
            // export as image
            using var encodedBitmapData = bitmap.EncodeAsJpeg(90);
            return encodedBitmapData.ToBytes();
        }
    }
    
    internal sealed class CompanionDocumentSnapshot
    {
        public ICollection<CompanionPageSnapshot> Pictures { get; set; }
        public CompanionCommands.UpdateDocumentStructure.DocumentHierarchyElement Hierarchy { get; set; }
    }
    
    internal sealed class CompanionDocumentCanvas : IDocumentCanvas, IDisposable
    {
        private ProxyDrawingCanvas DrawingCanvas { get; } = new();
        private Size CurrentPageSize { get; set; } = Size.Zero;

        private ICollection<CompanionPageSnapshot> PageSnapshots { get; } = new List<CompanionPageSnapshot>();
        
        internal CompanionCommands.UpdateDocumentStructure.DocumentHierarchyElement Hierarchy { get; set; }

        public CompanionDocumentSnapshot GetContent()
        {
            return new CompanionDocumentSnapshot
            {
                Pictures = PageSnapshots,
                Hierarchy = Hierarchy
            };
        }

        #region IDisposable
        
        ~CompanionDocumentCanvas()
        {
            this.WarnThatFinalizerIsReached();
            Dispose();
        }
        
        public void Dispose()
        {
            DrawingCanvas.Dispose();
            GC.SuppressFinalize(this);
        }
        
        #endregion
        
        #region IDocumentCanvas
        
        public void BeginDocument()
        {
            PageSnapshots.Clear();
        }

        public void EndDocument()
        {
            
        }

        public void BeginPage(Size size)
        {
            CurrentPageSize = size;
            
            DrawingCanvas.Target = new SkiaDrawingCanvas(size.Width, size.Height);
            DrawingCanvas.SetZIndex(0);
        }

        public void EndPage()
        {
            Debug.Assert(!CurrentPageSize.IsCloseToZero());
            
            using var pictureRecorder = new SkPictureRecorder();
            using var canvas = pictureRecorder.BeginRecording(CurrentPageSize.Width, CurrentPageSize.Height);

            using var snapshot = DrawingCanvas.GetSnapshot();
            snapshot.DrawOnSkCanvas(canvas);
            canvas.Save();
            
            var picture = pictureRecorder.EndRecording();
            PageSnapshots.Add(new CompanionPageSnapshot(picture, CurrentPageSize));
        }

        
        public IDrawingCanvas GetDrawingCanvas()
        {
            return DrawingCanvas;
        }
        
        #endregion
    }
}