// See https://aka.ms/new-console-template for more information
using Plotly.NET.CSharp;

// Super parameters
const int AGENT_COUNT = 1000;
const double AVG = 0.03;
const double STD = 1.0;
const double INITIAL_CASH = 10000.0;
const int INITIAL_ASSETS = 100;
const int STEP_COUNT = 300000;
const double INITIAL_PRICE = INITIAL_CASH / INITIAL_ASSETS;

var paramID = $"agCount{AGENT_COUNT}-avg{AVG:F3}-std{STD:F3}-initCash{INITIAL_CASH:F3}-initAss{INITIAL_ASSETS}-stepCount{STEP_COUNT}-initPrice{INITIAL_PRICE:F3}";


var agents = new Agent[AGENT_COUNT];
var rand = new Random(10);

for (int i = 0; i < agents.Length; i++)
{
    var agent = new Agent(cash: INITIAL_CASH, assets: INITIAL_ASSETS, p: rand.NextSingle());
    agents[i] = agent;
}

var price = INITIAL_PRICE;
var prices = new List<double>();

for (int i = 0; i < STEP_COUNT; i++)
{
    var orders = agents.Select(a => a.GetOrder(rand, AVG, STD, price)).ToArray();
    price = Math.Max(ClearingPrice(orders, 0f, 3 * price), 0.01);
    foreach (var order in orders)
        if (order.OrderType is OrderType.Buy && price <= order.LimitPrice)
            ExecOrder(order, price);
        else if (order.OrderType is OrderType.Sell && price >= order.LimitPrice)
            ExecOrder(order, price);
    if (i % 1000 is 0)
    {
        Console.Write(i.ToString().PadLeft(7, '0'));
        Console.WriteLine($": {price:F3}");
    }
    if (i % 10 is 0)
        prices.Add(price);
}

var chart = Chart.Line<int, double, string>(Enumerable.Range(0, prices.Count), prices)
.WithSize(Width: 1400);

Directory.CreateDirectory("output");
chart.SaveHtml($"./output/graph-{paramID}.html", false);
chart.SaveHtml($"./output/last-graph.html", true);

static double ClearingPrice(Order[] orders, double min, double max, int maxIter = 30)
{
    var price = (max + min) / 2;
    if (maxIter is <= 0) 
        return price;
    if (Demand(orders, price) > Supply(orders, price))
        return ClearingPrice(orders, price, max, maxIter - 1);
    else
        return ClearingPrice(orders, min, price, maxIter - 1);
}

static int Demand(Order[] orders, double price)
{
    var res = 0;
    foreach (var (_, limitPrice, q, ot) in orders)
        if (ot is OrderType.Buy && price <= limitPrice)
            res += q;
    return res;
}

static int Supply(Order[] orders, double price)
{
    var res = 0;
    foreach (var (_, limitPrice, q, ot) in orders)
        if (ot is OrderType.Sell && price >= limitPrice)
            res += q;
    return res;
}

static void ExecOrder(Order order, double price)
{
    if (order.OrderType is OrderType.Buy)
    {
        order.Agent.Cash -= price * order.Quantity;
        order.Agent.Assets += order.Quantity;
    }
    else
    {
        order.Agent.Cash += price * order.Quantity;
        order.Agent.Assets -= order.Quantity;
    }
}

enum OrderType
{
    Buy, Sell
}

readonly record struct Order(
    Agent Agent,
    double LimitPrice,
    int Quantity,
    OrderType OrderType);

sealed class Agent
{
    public double Cash { get; set; }
    public int Assets { get; set; }
    private double P { get; }

    public Agent(double cash, int assets, double p)
        => (Cash, Assets, P) = (cash, assets, p);

    public Order GetOrder(Random rand, double avg, double std, double currPrice)
    {
        var r = rand.NextSingle();
        if (rand.NextSingle() < P)
        {
            var limPrice = currPrice + rand.NextGaussian(mu: avg, sigma: std);
            var quantity = (int)(Cash / limPrice);
            return new Order(this, limPrice, quantity, OrderType.Buy);
        }
        else
        {
            var quantity = (int)(Assets * r);
            var limPrice = currPrice + rand.NextGaussian(mu: -avg, sigma: std);
            return new Order(this, limPrice, quantity, OrderType.Sell);
        }
    }
}

static class Ext
{
    public static double NextGaussian(this Random r, double mu = 0, double sigma = 1)
    {
        var u1 = r.NextDouble();
        var u2 = r.NextDouble();

        var rand_std_normal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                            Math.Sin(2.0 * Math.PI * u2);

        var rand_normal = mu + sigma * rand_std_normal;

        return rand_normal;
    }
}
