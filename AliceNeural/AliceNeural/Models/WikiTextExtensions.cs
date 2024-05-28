//using Javax.Xml.Transform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;


namespace AliceNeural.Models
{
    public static class WikitextHelper
    {

        /// <summary>
        /// Converte un testo in formato wikitext in testo leggibile
        /// </summary>
        /// <param name="wikitext">Il wikitext in input</param>
        /// <returns>La stringa corrispondente al testo leggibile</returns>
        public static string WikiTextToReadableText(this string wikitext)
        {
            //https://github.com/CXuesong/MwParserFromScratch
            var parser = new WikitextParser();
            var ast = parser.Parse(wikitext);
            StringBuilder sb = new();
            foreach (var line in ast.Lines)
            {

                if (line.GetType() == typeof(Paragraph) && line.ToPlainText().StartsWith("[["))
                    continue;//non inserisco i paragrafi delle illustrazioni
                var children = line.EnumChildren();
                foreach (var child in children)
                {
                    string childText = child.ToPlainText();
                    if (child.GetType() == typeof(Template))//in questo caso si effettua un parsing manuale, ad esempio per gestire: https://www.mediawiki.org/wiki/Help:Magic_words#Formatting
                    {
                        string? elemento = child.ToString();
                        List<string> paroleChiave = ["vedi"];
                        string? result = elemento != null ? paroleChiave.FirstOrDefault((_) => elemento.Contains(_, StringComparison.CurrentCultureIgnoreCase)) : null;
                        if (elemento != null && result == null)//il child non contiene le parole chiave
                        {
                            //posso inserire il contenuto
                            int startIndex = elemento.IndexOfAny([' ', ':']);//l'ordine conta
                            startIndex = (startIndex == -1) ? elemento.LastIndexOf('{') : startIndex;//se non trovo il punto di inizio del contenuto assumo che ci siano due {{
                            int stopIndex = elemento.IndexOfAny(['|', '}']);//l'ordine conta
                            int lunghezzaContenuto = stopIndex - startIndex - 1;
                            string contenuto = elemento.Substring(startIndex + 1, lunghezzaContenuto);
                            sb.Append(contenuto);
                        }
                    }
                    else if (child.GetType() == typeof(WikiLink))//quando è un WikiLink non stampo i riferimenti a file, thumb e alt, etc.
                    {
                        List<string> paroleChiave = new() { "file", "thumb", "alt" };
                        string? result = paroleChiave.FirstOrDefault((_) => childText.Contains(_, StringComparison.CurrentCultureIgnoreCase));
                        if (result == null)//il childText non contiene le parole chiave
                        {
                            sb.Append(childText);
                        }
                    }
                    else //child.GetType()!= WikiLink && child.GetType()!=Template
                    {
                        sb.Append(childText);
                    }
                }
                if (line.GetType() == typeof(Heading) || line.GetType() == typeof(Paragraph))//quando sono alla fine di un'intestazione oppure di un paragrafo decido se andare a capo
                {
                    if (!string.IsNullOrWhiteSpace(line.ToPlainText()))
                    {//controllo che ci sia un contenuto nella riga corrente per andare a capo
                        sb.Append('\n');
                    }
                }
            }
            return sb.ToString();
        }
        /// <summary>
        /// Converte un testo in formato wikitext in testo leggibile. 
        /// Per ogni linea nel wikitext viene restituita una stringa risultato.
        /// A differenza del metodo WikiTextToReadableText, questo restituisce un array di stringhe
        /// </summary>
        /// <param name="wikitext">Il wikitext in input</param>
        /// <returns>Un array di stringhe, ciascuna con testo leggibile. Ogni stringa restituita corrisponde a una linea all'interno del AST (Abstract Syntax Tree) del wikitext parser</returns>
        public static string[] WikiTextToReadableTextArray(this string wikitext)
        {
            //https://github.com/CXuesong/MwParserFromScratch
            var parser = new WikitextParser();
            var ast = parser.Parse(wikitext);
            StringBuilder sb = new();
            List<string> readableLines = new();
            foreach (var line in ast.Lines)
            {
                sb.Clear();//ripulisco lo StringBuilder per la linea in corso
                if (line.GetType() == typeof(Paragraph) && line.ToPlainText().StartsWith("[["))
                    continue;//non inserisco i paragrafi delle illustrazioni
                var children = line.EnumChildren();
                foreach (var child in children)
                {
                    string childText = child.ToPlainText();
                    if (string.IsNullOrWhiteSpace(childText) || childText.Equals("\n"))//non inserisco elementi vuoti o con solo a capo
                    {
                        continue;
                    }
                    else if (child.GetType() == typeof(Template))//in questo caso si effettua un parsing manuale, ad esempio per gestire: https://www.mediawiki.org/wiki/Help:Magic_words#Formatting
                    {
                        string? elemento = child.ToString();
                        List<string> paroleChiave = ["vedi"];
                        string? result = elemento != null ? paroleChiave.FirstOrDefault((_) => elemento.Contains(_, StringComparison.CurrentCultureIgnoreCase)) : null;
                        if (elemento != null && result == null)//il child non contiene le parole chiave
                        {
                            //posso inserire il contenuto
                            int startIndex = elemento.IndexOfAny([' ', ':']);//l'ordine conta
                            startIndex = (startIndex == -1) ? elemento.LastIndexOf('{') : startIndex;//se non trovo il punto di inizio del contenuto assumo che ci siano due {{
                            int stopIndex = elemento.IndexOfAny(['|', '}']);//l'ordine conta
                            int lunghezzaContenuto = stopIndex - startIndex - 1;
                            string contenuto = elemento.Substring(startIndex + 1, lunghezzaContenuto);
                            sb.Append(contenuto);
                        }
                    }
                    else if (child.GetType() == typeof(WikiLink))//quando è un WikiLink non stampo i riferimenti a file, thumb e alt, etc.
                    {
                        List<string> paroleChiave = ["file", "thumb", "alt"];
                        string? result = paroleChiave.FirstOrDefault((_) => childText.Contains(_, StringComparison.CurrentCultureIgnoreCase));
                        if (result == null)//il childText non contiene le parole chiave
                        {
                            sb.Append(childText);
                        }
                    }
                    else //child.GetType()!= WikiLink && child.GetType()!=Template
                    {
                        sb.Append(childText);
                    }
                }
                if (sb.Length > 0)
                {
                    readableLines.Add(sb.ToString());
                }

            }
            return [.. readableLines];
        }
        /// <summary>
        /// Converte un testo in formato wikitext in testo leggibile. Vengono rimossi gli spazi corrispondenti a new line
        /// </summary>
        /// <param name="wikitext">Il wikitext in input</param>
        /// <returns>La stringa corrispondente al testo leggibile</returns>
        public static string WikiTextToReadableTextNoSpace(this string wikitext)
        {
            //https://github.com/CXuesong/MwParserFromScratch
            var parser = new WikitextParser();
            var ast = parser.Parse(wikitext);
            StringBuilder sb = new();
            foreach (var line in ast.Lines)
            {
                if (line.GetType() == typeof(Paragraph) && line.ToPlainText().StartsWith("[["))
                    continue;//non inserisco i paragrafi delle illustrazioni
                var children = line.EnumChildren();
                foreach (var child in children)
                {
                    string childText = child.ToPlainText();
                    if (string.IsNullOrWhiteSpace(childText) || childText.Equals("\n"))
                    {
                        continue;
                    }
                    else if (child.GetType() == typeof(Template))//in questo caso si effettua un parsing manuale, ad esempio per gestire: https://www.mediawiki.org/wiki/Help:Magic_words#Formatting
                    {
                        string? elemento = child.ToString();
                        List<string> paroleChiave = ["vedi"];
                        string? result = elemento != null ? paroleChiave.FirstOrDefault((_) => elemento.Contains(_, StringComparison.CurrentCultureIgnoreCase)) : null;
                        if (elemento != null && result == null)//il child non contiene le parole chiave
                        {
                            //posso inserire il contenuto
                            int startIndex = elemento.IndexOfAny([' ', ':']);//l'ordine conta
                            startIndex = (startIndex == -1) ? elemento.LastIndexOf('{') : startIndex;//se non trovo il punto di inizio del contenuto assumo che ci siano due {{
                            int stopIndex = elemento.IndexOfAny(['|', '}']);//l'ordine conta
                            int lunghezzaContenuto = stopIndex - startIndex - 1;
                            string contenuto = elemento.Substring(startIndex + 1, lunghezzaContenuto);
                            sb.Append(contenuto);
                        }
                    }
                    else if (child.GetType() == typeof(WikiLink))//quando è un WikiLink non stampo i riferimenti a file, thumb e alt, etc.
                    {
                        List<string> paroleChiave = ["file", "thumb", "alt"];
                        string? result = paroleChiave.FirstOrDefault((_) => childText.Contains(_, StringComparison.CurrentCultureIgnoreCase));
                        if (result == null)//il childText non contiene le parole chiave
                        {
                            sb.Append(childText);
                        }
                    }
                    else //child.GetType()!= WikiLink && child.GetType()!=Template
                    {
                        sb.Append(childText);
                    }
                }
                //if (line.GetType() == typeof(Heading) || line.GetType() == typeof(Paragraph))//quando sono alla fine di un'intestazione oppure di un paragrafo decido se andare a capo
                //{
                //    if (!string.IsNullOrWhiteSpace(line.ToPlainText()))
                //    {//controllo che ci sia un contenuto nella riga corrente per andare a capo
                //        sb.Append('\n');
                //    }
                //}
            }
            return sb.ToString();
        }
        /// <summary>
        /// Effettua lo split di una stringa di testo in un array di stringhe, usando il punto come separatore. 
        /// Il punto non è rimosso dal risultato
        /// </summary>
        /// <param name="text">Testo che si vuole suddividere in più stringhe</param>
        /// <returns>Array di stringhe ottenuto facendo lo split sul punto</returns>
        public static string[] SplitOnPeriod(this string text)
        {
            //return text.Split(separators, StringSplitOptions.RemoveEmptyEntries);//questo funziona,ma toglie i punti

            List<string> afterSplit = [];//lista contenente le frasi separate da .
            int start = 0;//indice di inizio frase
            for (int counter = 0; counter < text.Length; counter++)
            {
                if (text[counter] == '.')//se trovo un punto
                {
                    int lineLength = counter - start + 1;
                    afterSplit.Add(text.Substring(start, lineLength));
                    start = counter + 1;//aggiorno start
                }
            }
            if (start < text.Length)//se, uscito dal ciclo, start è < di text.Length vuol dire che l'ultimo pezzo del testo non è stato incluso, perché non c'era il punto finale
            {
                afterSplit.Add(text[start..]);
            }
            return [.. afterSplit];
        }
    }
}
