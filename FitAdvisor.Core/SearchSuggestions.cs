namespace FitAdvisor.Core;
public static class SearchSuggestions
{
    public static bool Near(string haystack,string query) {
        var term=FoodCatalogue.Normalize(query.Trim());if(term.Length<4||term.Length>40||term.Contains(' '))return false;
        return FoodCatalogue.Normalize(haystack).Split(new[]{' ', '/', ',', '(', ')'},StringSplitOptions.RemoveEmptyEntries).Any(word=>Distance(word,term)<=1);
    }
    private static int Distance(string a,string b) {
        if(Math.Abs(a.Length-b.Length)>1)return 99;
        var row=Enumerable.Range(0,b.Length+1).ToArray();
        for(var i=1;i<=a.Length;i++){var next=new int[b.Length+1];next[0]=i;for(var j=1;j<=b.Length;j++)next[j]=Math.Min(Math.Min(next[j-1]+1,row[j]+1),row[j-1]+(a[i-1]==b[j-1]?0:1));row=next;}return row[b.Length];
    }
}
