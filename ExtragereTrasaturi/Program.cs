using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

class Document
{
    public string Id = "";
    public string Folder = "";
    public List<string> Topics = new List<string>();
    public Dictionary<string, int> Counts = new Dictionary<string, int>();
}

class Program
{
    static void Main()
    {
        // Cai (merge si daca rulezi din folderul bin/Debug)
        string caleStopwords = File.Exists("stopwords.txt") ? "stopwords.txt" : "../stopwords.txt";
        string bazaReuters = Directory.Exists("Reuters") ? "Reuters" : "../Reuters";
        string fisierIesire = "vectori.txt";
        string folderArticole = Directory.Exists("Reuters") ? "Articole_curatate" : "../Articole_curatate";
        string delimitator = " ";
        Directory.CreateDirectory(folderArticole);

        var stopwords = new HashSet<string>(
            File.ReadAllLines(caleStopwords)
                .Select(w => w.Trim().ToLower())
                .Where(w => w.Length > 0));

        var stemmer = new WordFrequency.PorterStemmer();

        var documente = new List<Document>();
        var dictionar = new SortedSet<string>(StringComparer.Ordinal); // vectorul global

        var optiuni = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive };

        // PASUL 1: parcurgem toate articolele, numaram cuvintele, construim dictionarul
        foreach (var folder in new[] { "Training", "Testing" })
        {
            string caleFolder = Path.Combine(bazaReuters, "Reuters_34", folder);
            string subfolderArticole = Path.Combine(folderArticole, folder);
            Directory.CreateDirectory(subfolderArticole);

            foreach (var fisier in Directory.EnumerateFiles(caleFolder, "*.xml", optiuni))
            {
                var doc = Citeste(fisier, folder, stopwords, stemmer, subfolderArticole, delimitator);
                documente.Add(doc);
                foreach (var cuvant in doc.Counts.Keys)
                    dictionar.Add(cuvant);
            }
        }

        // PASUL 2: pozitia fiecarui cuvant in vectorul global
        var pozitii = new Dictionary<string, int>();
        int index = 0;
        foreach (var cuvant in dictionar)
            pozitii[cuvant] = index++;

        // PASUL 3: scriem fisierul
        using var sw = new StreamWriter(fisierIesire, false, new UTF8Encoding(false));

        foreach (var cuvant in dictionar)
            sw.WriteLine("@attribute " + cuvant);

        sw.WriteLine("@data");

        foreach (var d in documente)
        {
            // vector rar: pozitie:frecventa, in ordine crescatoare a pozitiilor
            var vectorRar = d.Counts
                .Select(kv => new { Poz = pozitii[kv.Key], Frecv = kv.Value })
                .OrderBy(x => x.Poz)
                .Select(x => x.Poz + ":" + x.Frecv);

            sw.WriteLine(d.Id + " # " + string.Join(" ", vectorRar) +
                         " # " + string.Join(" ", d.Topics) +
                         " # " + d.Folder);
        }

        Console.WriteLine($"Gata: {documente.Count} articole, {dictionar.Count} cuvinte in dictionar.");
    }

    static Document Citeste(string fisier, string folder,
                        HashSet<string> stopwords, WordFrequency.PorterStemmer stemmer,
                        string subfolderArticole, string delimitator)
    {
        var xml = XDocument.Load(fisier);
        var rezultat = new Document
        {
            Id = Path.GetFileNameWithoutExtension(fisier),
            Folder = folder
        };

        // titlu + paragrafele din <text>
        string titlu = xml.Descendants("title").FirstOrDefault()?.Value.Trim() ?? "";
        string paragrafe = string.Join(" ",
            xml.Descendants("text").Descendants("p").Select(p => p.Value));

        // topicurile (doar codurile)
        rezultat.Topics = xml.Descendants("codes")
            .Where(c => (string?)c.Attribute("class") == "bip:topics:1.0")
            .Descendants("code")
            .Select(c => (string?)c.Attribute("code") ?? "")
            .Where(s => s.Length > 0)
            .ToList();

        // curatare: litere mici, fara cifre/punctuatie, fara stopwords, cu stemming
        string text = titlu + " " + paragrafe;
        var caractere = text.Select(c => char.IsLetter(c) ? char.ToLower(c) : ' ').ToArray();
        var cuvinte = new string(caractere)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1 && !stopwords.Contains(w))
            .Select(w => stemmer.StemWord(w))
            .Where(w => w.Length > 0)
            .ToList();

        foreach (var cuvant in cuvinte)
        {
            rezultat.Counts.TryGetValue(cuvant, out int n);
            rezultat.Counts[cuvant] = n + 1;
        }

        string caleArticol = Path.Combine(subfolderArticole, rezultat.Id + ".txt");
        string continutFisier = "Titlu: " + titlu + Environment.NewLine +
                                "Categorii: " + string.Join(" ", rezultat.Topics) + Environment.NewLine +
                                "Cuvinte: " + string.Join(delimitator, cuvinte);
        File.WriteAllText(caleArticol, continutFisier);

        return rezultat;
    }
}