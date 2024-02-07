// See https://aka.ms/new-console-template for more information
using System.Text;

Console.WriteLine("Hello, World!");


var http = new HttpClient();
var resp = http.PostAsync("http://localhost:3000/Issuer/TokenCall", new StringContent("{}", Encoding.UTF8, "application/json")).Result;

Console.WriteLine(resp);
