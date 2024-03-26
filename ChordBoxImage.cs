/*
 * Chord Image Generator
 * https://einaregilsson.com/chord-image-generator/
 *
 * Copyright (C) 2009-2019 Einar Egilsson [einar@einaregilsson.com]
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the “Software”), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
 * IN THE SOFTWARE.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// NOTE 2019-08-27: This code was written more than 10 years ago. I'm updating
// the surrounding stuff now, getting everything running on .NET Core, just to
// let this project live on, but the actual chord drawing code is kind of strange
// in lots of ways and probably not how I would write it today. At the same time
// it does work and I'm not really motivated enough to rewrite it :)

namespace EinarEgilsson.Chords
{

    /// <summary>
    /// Class to create images of chordboxes. Can be saved
    /// to any subclass of Stream.
    /// </summary>
    public class ChordBoxImage : IDisposable
    {

        #region Constants

        const char NO_FINGER = '-';
        const char THUMB = 'T';
        const char INDEX_FINGER = '1';
        const char MIDDLE_FINGER = '2';
        const char RING_FINGER = '3';
        const char LITTLE_FINGER = '4';
        const int OPEN = 0;
        const int MUTED = -1;
        const int FRET_COUNT = 5;
        const string FONT_NAME = "DejaVu Sans";

        #endregion

        #region Fields
        private Bitmap _bitmap;
        private Graphics _graphics;

        private int _size;
        private int[] _chordPositions = new int[6];
        private char[] _fingers = new char[] { NO_FINGER, NO_FINGER, NO_FINGER,
                                             NO_FINGER, NO_FINGER, NO_FINGER};
        private readonly bool _drawFullBarre;
        private string _chordName;
        private bool _parseError;

        private float _fretWidth;
        private int _lineWidth;
        private float _boxWidth;
        private float _boxHeight;

        private int _imageWidth;
        private int _imageHeight;
        // upper corner of the chordbox
        private float _xstart;
        private float _ystart;
        private float _nutHeight;

        private int _dotWidth;
        private float _signWidth;
        private float _signRadius;

        // Different font sizes
        private float _fretFontSize;
        private float _fingerFontSize;
        private float _nameFontSize;
        private float _superScriptFontSize;
        private float _markerWidth;

        private Brush _foregroundBrush = Brushes.Black;
        private Brush _backgroundBrush = Brushes.White;

        private int _baseFret;

        #endregion

        #region Constructor

        public ChordBoxImage(string name, string chord, string fingers, string size, bool drawFullBarre = false)
        {
            _drawFullBarre = drawFullBarre;
            _chordName = ParseName(name);
            ParseChord(chord);
            ParseFingers(fingers);
            ParseSize(size);
            InitializeSizes();
            CreateImage();
        }

        #endregion

        #region Public methods

        public async Task SaveAsync(Stream output)
        {
            using (var ms = new MemoryStream())
            {
                _bitmap.Save(ms, ImageFormat.Png);
                ms.Seek(0, SeekOrigin.Begin);
                await ms.CopyToAsync(output);
            }
        }

        public void Save(Stream output)
        {
            var awaiter = SaveAsync(output).ConfigureAwait(false).GetAwaiter();
            awaiter.GetResult();
        }

        public byte[] GetBytes()
        {
            using (var ms = new MemoryStream())
            {
                Save(ms);
                ms.Seek(0, SeekOrigin.Begin);
                byte[] buffer = new byte[ms.Length];
                ms.Read(buffer, 0, buffer.Length);
                return buffer;
            }
        }


        public void Dispose()
        {
            _bitmap.Dispose();
        }

        #endregion

        #region Private methods

        private string ParseName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }

            var splitString = name.Split('_');
            for (int i = 1; i < splitString.Length; i++)
            {
                if (i % 2 == 0)
                {
                    continue;
                }

                if (splitString[i].Length > 1)
                {
                    continue;
                }

                splitString[i] = splitString[i]
                    .Replace("#", "♯")
                    .Replace("b", "♭")
                    .Replace("B", "♭");
            }
            return string.Join("_", splitString);
        }

        private void ParseChord(string chord)
        {
            // If the chord involves frets higher than 10, the positions must be
            // separated by a dash, e.g. 10-12-12-0-0-0.
            if (chord == null || !Regex.IsMatch(chord, @"[\dxX]{6}|((1|2)?[\dxX]-){5}(1|2)?[\dxX]"))
            {
                _parseError = true;
                return;
            }

            var positions = GetFretPositions(chord);
            for (int i = 0; i < 6; i++)
            {
                if (positions[i].ToUpper() == "X")
                {
                    _chordPositions[i] = MUTED;
                }
                else
                {
                    _chordPositions[i] = int.Parse(positions[i]);
                }
            }

            SetBaseFret();

            // This is a local function, to prevent anybody from calling this
            // without setting _chordPositions first.
            void SetBaseFret()
            {
                // We're differentiating, because we can always play an open
                // chord, no matter how high up on the fretboard we are.
                // However, we have to also consider the case that we're playing
                // all open strings or even all muted strings (i.e. everything
                // is 0 or -1).
                var nonZeroChordPositions = _chordPositions
                    .Where(p => p > 0);
                var minFret = nonZeroChordPositions.Any()
                    ? nonZeroChordPositions.Min()
                    : 0;

                // The highest fret is easy. :-)
                var maxFret = _chordPositions
                    .Max();

                if (maxFret <= 5)
                {
                    _baseFret = 1;
                }
                else
                {
                    _baseFret = minFret;
                }
            }
        }


        private string[] GetFretPositions(string chordInput)
        {
            if (chordInput.Length > 6)
            {
                return chordInput.Split('-');
            }

            var parts = new string[6];
            for (int i = 0; i < 6; i++)
            {
                parts[i] = chordInput[i].ToString();
            }
            return parts;
        }

        private void ParseFingers(string fingers)
        {
            if (fingers == null)
            {
                // Allowed to not specify fingers
                return;
            }
            else if (!Regex.IsMatch(fingers, @"[tT\-1234]{6}"))
            {
                _parseError = true;
            }
            else
            {
                _fingers = fingers.ToUpper().ToCharArray();
            }
        }

        private void ParseSize(string size)
        {
            if (double.TryParse(size, out var dsize))
            {
                dsize = Math.Round(dsize, 0);
                _size = Convert.ToInt32(Math.Min(Math.Max(1, dsize), 10));
            }
            else
            {
                _size = 1;
            }
        }

        private void InitializeSizes()
        {
            _fretWidth = 4 * _size;
            _nutHeight = _fretWidth / 2f;
            _lineWidth = (int)Math.Ceiling(_size * 0.31);
            _dotWidth = (int)Math.Ceiling(0.9 * _fretWidth);
            _markerWidth = 0.7f * _fretWidth;
            _boxWidth = 5 * _fretWidth + 6 * _lineWidth;
            _boxHeight = FRET_COUNT * (_fretWidth + _lineWidth) + _lineWidth;

            // Find out font sizes
            FontFamily family = new FontFamily(FONT_NAME);
            float perc = family.GetCellAscent(FontStyle.Regular) / (float)family.GetLineSpacing(FontStyle.Regular);
            _fretFontSize = _fretWidth / perc;
            _fingerFontSize = _fretWidth * 0.8f;
            _nameFontSize = _fretWidth * 2f / perc;
            _superScriptFontSize = 0.7f * _nameFontSize;

            if (_size == 1)
            {
                _nameFontSize += 2;
                _fingerFontSize += 2;
                _fretFontSize += 2;
                _superScriptFontSize += 2;
            }

            if (string.IsNullOrEmpty(_chordName))
            {
                _ystart = (float)Math.Round(_nutHeight + 1.7f * _markerWidth);
            }
            else
            {
                _ystart = (float)Math.Round(0.2f * _superScriptFontSize + _nameFontSize + _nutHeight + 1.7f * _markerWidth);
            }
            _imageWidth = (int)(_boxWidth + 5 * _fretWidth);
            _imageHeight = (int)(_boxHeight + _ystart + _fretWidth + _fretWidth);

            _signWidth = (int)(_fretWidth * 0.75);
            _signRadius = _signWidth / 2;
        }

        private void CreateImage()
        {
            _bitmap = new Bitmap(_imageWidth, _imageHeight);
            _graphics = Graphics.FromImage(_bitmap);
            _graphics.SmoothingMode = SmoothingMode.HighQuality;

            // Widen image if the chord name won't fit.
            var chordNameSize = getChordNameSizeInPixels();
            if (_imageWidth < chordNameSize + 2 * _fretWidth)
            {
                _imageWidth = (int) (chordNameSize + 2 * _fretWidth);
                _bitmap = new Bitmap(_imageWidth, _imageHeight);
                _graphics = Graphics.FromImage(_bitmap);
                _graphics.SmoothingMode = SmoothingMode.HighQuality;
            }

            // We can only do this now. This is a chicken-egg problem.
            // We need _graphics to measure the text, but we can only get graphics once we've initialized the bitmap.
            // And only once we have the final image width, can we center the fretboard box.
            _xstart = _imageWidth / 2 - _boxWidth / 2;

            _graphics.FillRectangle(_backgroundBrush, 0, 0, _bitmap.Width, _bitmap.Height);
            if (_parseError)
            {
                // Draw red x
                Pen errorPen = new Pen(Color.Red, 3f);
                _graphics.DrawLine(errorPen, 0f, 0f, _bitmap.Width, _bitmap.Height);
                _graphics.DrawLine(errorPen, 0f, _bitmap.Height, _bitmap.Width, 0);
            }
            else
            {
                DrawChordBox();
                DrawChordPositions();
                DrawChordName();
                DrawFingers();
                DrawBars();
            }
        }

        private void DrawChordBox()
        {
            Pen pen = new Pen(_foregroundBrush, _lineWidth);
            float totalFretWidth = _fretWidth + _lineWidth;
            for (int i = 0; i <= FRET_COUNT; i++)
            {
                float y = _ystart + i * totalFretWidth;
                _graphics.DrawLine(pen, _xstart, y, _xstart + _boxWidth - _lineWidth, y);
            }

            for (int i = 0; i < 6; i++)
            {
                float x = _xstart + (i * totalFretWidth);
                _graphics.DrawLine(pen, x, _ystart, x, _ystart + _boxHeight - pen.Width);
            }

            if (_baseFret == 1)
            {
                // Need to draw the nut
                float nutHeight = _fretWidth / 2f;
                _graphics.FillRectangle(_foregroundBrush, _xstart - _lineWidth / 2f, _ystart - nutHeight, _boxWidth, nutHeight);
            }
        }

        private void DrawChordPositions()
        {
            float yoffset = _ystart - _fretWidth;
            float xoffset = _lineWidth / 2f;
            float totalFretWidth = _fretWidth + _lineWidth;
            float xfirstString = _xstart + 0.5f * _lineWidth;
            for (int i = 0; i < _chordPositions.Length; i++)
            {
                int absolutePos = _chordPositions[i];
                int relativePos = absolutePos - _baseFret + 1;

                float xpos = _xstart - (0.5f * _fretWidth) + (0.5f * _lineWidth) + (i * totalFretWidth);
                if (relativePos > 0)
                {
                    float ypos = relativePos * totalFretWidth + yoffset;
                    _graphics.FillEllipse(_foregroundBrush, xpos, ypos, _dotWidth, _dotWidth);
                }
                else if (absolutePos == OPEN)
                {
                    Pen pen = new Pen(_foregroundBrush, _lineWidth);
                    float ypos = _ystart - _fretWidth;
                    float markerXpos = xpos + ((_dotWidth - _markerWidth) / 2f);
                    if (_baseFret == 1)
                    {
                        ypos -= _nutHeight;
                    }
                    _graphics.DrawEllipse(pen, markerXpos, ypos, _markerWidth, _markerWidth);
                }
                else if (absolutePos == MUTED)
                {
                    Pen pen = new Pen(_foregroundBrush, _lineWidth);
                    float ypos = _ystart - _fretWidth;
                    float markerXpos = xpos + ((_dotWidth - _markerWidth) / 2f);
                    if (_baseFret == 1)
                    {
                        ypos -= _nutHeight;
                    }
                    _graphics.DrawLine(pen, markerXpos, ypos, markerXpos + _markerWidth, ypos + _markerWidth);
                    _graphics.DrawLine(pen, markerXpos, ypos + _markerWidth, markerXpos + _markerWidth, ypos);
                }
            }
        }

        private void DrawChordName()
        {
            Font nameFont = new Font(FONT_NAME, _nameFontSize, GraphicsUnit.Pixel);
            Font superFont = new Font(FONT_NAME, _superScriptFontSize, GraphicsUnit.Pixel);
            string[] parts = _chordName.Split('_');

            // Set max parts to 4 for protection
            int maxParts = parts.Length;
            if (maxParts > 4)
            {
                maxParts = 4;
            }

            var chordNameSize = getChordNameSizeInPixels();

            // set the x position for the chord name
            var xTextStart = _imageWidth / 2 - chordNameSize / 2;

            // Paint the chord
            for (int i = 0; i < maxParts; i++)
            {
                if (i % 2 == 0)
                {
                    SizeF stringSize2 = _graphics.MeasureString(parts[i], nameFont);
                    _graphics.DrawString(parts[i], nameFont, _foregroundBrush, xTextStart, 0.2f * _superScriptFontSize);
                    xTextStart += stringSize2.Width;
                }
                else
                {
                    SizeF stringSize2 = _graphics.MeasureString(parts[i], superFont);
                    _graphics.DrawString(parts[i], superFont, _foregroundBrush, xTextStart, 0);
                    xTextStart += stringSize2.Width;
                }
            }

            if (_baseFret > 1)
            {
                Font fretFont = new Font(FONT_NAME, _fretFontSize, GraphicsUnit.Pixel);
                float offset = (fretFont.Size - _fretWidth) / 2f;
                _graphics.DrawString(_baseFret + "fr", fretFont, _foregroundBrush, _xstart + _boxWidth + 0.3f * _fretWidth, _ystart - offset);
            }
        }

        private float getChordNameSizeInPixels()
        {
            Font nameFont = new Font(FONT_NAME, _nameFontSize, GraphicsUnit.Pixel);
            Font superFont = new Font(FONT_NAME, _superScriptFontSize, GraphicsUnit.Pixel);
            string[] parts = _chordName.Split('_');

            // Set max parts to 4 for protection
            int maxParts = parts.Length;
            if (maxParts > 4)
            {
                maxParts = 4;
            }
            // count total width of the chord in pixels
            float chordNameSize = 0;
            for (int i = 0; i < maxParts; i++)
            {
                if (i % 2 == 0)
                {
                    // odd parts are normal text
                    SizeF stringSize2 = _graphics.MeasureString(parts[i], nameFont);
                    chordNameSize += stringSize2.Width;
                }
                else
                {
                    // even parts are superscipts
                    SizeF stringSize2 = _graphics.MeasureString(parts[i], superFont);
                    chordNameSize += stringSize2.Width;
                }
            }
            return chordNameSize;
        }

        private void DrawFingers()
        {
            float xpos = _xstart + (0.5f * _lineWidth);
            float ypos = _ystart + _boxHeight;
            Font font = new Font(FONT_NAME, _fingerFontSize);
            foreach (char finger in _fingers)
            {
                if (finger != NO_FINGER)
                {
                    SizeF charSize = _graphics.MeasureString(finger.ToString(), font);
                    _graphics.DrawString(finger.ToString(), font, _foregroundBrush, xpos - (0.5f * charSize.Width), ypos);
                }
                xpos += (_fretWidth + _lineWidth);
            }
        }

        private struct Bar { public int Str, Pos, Length; public char Finger; }

        private void DrawBars()
        {
            var bars = new Dictionary<char, Bar>();
            var firstBarre = true;
            for (int i = 0; i < 5; i++)
            {
                if (_chordPositions[i] != MUTED && _chordPositions[i] != OPEN && _fingers[i] != NO_FINGER && !bars.ContainsKey(_fingers[i]))
                {
                    Bar bar = new Bar { Str = i, Pos = _chordPositions[i], Length = 0, Finger = _fingers[i] };
                    if (_drawFullBarre && firstBarre)
                    {
                        bar.Length = 5 - bar.Str;
                        firstBarre = false;
                    }
                    else
                    {
                        for (int j = i + 1; j < 6; j++)
                        {
                            if (_fingers[j] == bar.Finger && _chordPositions[j] == _chordPositions[i])
                            {
                                bar.Length = j - i;
                            }
                        }
                    }
                    if (bar.Length > 0)
                    {
                        bars.Add(bar.Finger, bar);
                    }
                }
            }

            float totalFretWidth = _fretWidth + _lineWidth;
            float arcWidth = _dotWidth / 7;
            foreach (Bar bar in bars.Values)
            {
                float yTempOffset = 0.0f;

                if (bar.Pos == 1)
                {
                    // the bar must go a little higher in order to be shown correctly
                    yTempOffset = -0.3f * totalFretWidth;
                }

                float xstart = _xstart + bar.Str * totalFretWidth - (_dotWidth / 2);
                float y = _ystart + (bar.Pos - _baseFret) * totalFretWidth - (0.6f * totalFretWidth) + yTempOffset;
                Pen pen = new Pen(_foregroundBrush, arcWidth);
                Pen pen2 = new Pen(_foregroundBrush, 1.3f * arcWidth);

                float barWidth = bar.Length * totalFretWidth + _dotWidth;

                _graphics.DrawArc(pen, xstart, y, barWidth, totalFretWidth, -1, -178);
                _graphics.DrawArc(pen2, xstart, y - arcWidth, barWidth, totalFretWidth + arcWidth, -4, -172);
                _graphics.DrawArc(pen2, xstart, y - 1.5f * arcWidth, barWidth, totalFretWidth + 3 * arcWidth, -20, -150);
            }
        }

        #endregion
    }
}
