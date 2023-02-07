# Chord Image Generator

> Attention!
>
> This is forked from [https://github.com/einaregilsson/ChordImageGenerator](https://github.com/einaregilsson/ChordImageGenerator) to keep the project maintained.

Chord Image Generator is a small .NET library to generate images of guitar chords. It can display chord boxes, starting frets, barred chords, fingerings and open and muted strings.

Here's a [blog post](https://einaregilsson.com/chord-image-generator/) explaining more about the library.


![Image of a D Chord](https://chordgenerator.luetzners.de/D.png?p=xx0232&f=---132&s=3 "D Chord")
![Image of a A Chord](https://chordgenerator.luetzners.de/A.png?p=x02220&f=--123-&s=3 "A Chord")
![Image of a A Chord](https://chordgenerator.luetzners.de/A_5.png?p=577655&f=134211&s=3 "A bar Chord")

There is a small example website at [https://chordgenerator.luetzners.de](https://chordgenerator.luetzners.de) where you can try different chords and see how they are constructed. But basically it is just done by constructing the right url,
name of the file is the name of the chord, finger positions (p), finger numbers (f) and size (s) are querystring parameters. E.g. the chords above are the following urls:

* [https://chordgenerator.luetzners.de/D.png?p=xx0232&f=---132&s=3](https://chordgenerator.luetzners.de/D.png?p=xx0232&f=---132&s=3)
* [https://chordgenerator.luetzners.de/A.png?p=x02220&f=--123-&s=3](https://chordgenerator.luetzners.de/A.png?p=x02220&f=--123-&s=3)
* [https://chordgenerator.luetzners.de/A_5.png?p=577655&f=134211&s=3](https://chordgenerator.luetzners.de/A_5.png?p=577655&f=134211&s=3)

There is nothing web specific about the actual image generation. It can be saved to any stream so you could just as easily use it in a desktop application.

## Using the project

This project has been updated to use .NET Core 6.0. Clone the repository from GitHub, then go into the folder where it is and run:

```
dotnet restore
dotnet run
```

That should start a local web server on port 5000, access it as http://localhost:5000/ .

If you're on a Mac you will need to install mono-libgdiplus like this: ```brew install mono-libgdiplus```.

If you're on Linux, you'll need to install libgdiplus, e.g. ```sudo apt install libgdiplus```.

The project should run on a Mac and on Linux but the drawing might not look **exactly** the same as it does on Windows, because of GDI+ differences.

The source is licensed under the MIT License. You are also free to link directly to chordgenerator.luetzners.de, although I give no guarantees about uptime or reliability.

## Running with Docker

You can also run this web server as a Docker container.

```bash
docker run -p 5000:80 rluetzner/chordimagegenerator:latest
```
