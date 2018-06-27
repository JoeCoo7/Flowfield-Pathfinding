using NUnit.Framework;
using Unity.Mathematics;

public class GridUtiltiesTests
{
    static int2 Invalid_Grid = new int2(-1, -1);
    static int Invalid_Index = -1;

    [Test]
    public void I2G_ValidWithSqureGrids()
    {
        var squareGrid = new int2(512, 512);
        TestGrid(squareGrid);
    }

    [Test]
    public void I2G_ValidWithNonSqureGrids_XGreaterThanY()
    {
        var squareGrid = new int2(512, 256);
        TestGrid(squareGrid);
    }

    [Test]
    public void I2G_ValidWithNonSqureGrids_YGreaterThanX()
    {
        var squareGrid = new int2(256, 512);
        TestGrid(squareGrid);
    }

    [Test]
    public void I2G_ValidWithNonPowerOf2Grids()
    {
        var squareGrid = new int2(7, 7);
        TestGrid(squareGrid);
    }

    static void TestGrid(int2 grid)
    {
        int min = 0;
        int max = grid.x * grid.y - 1;

        int edge = grid.x - 1;

        // Bounds Checks
        Assert.AreEqual(new int2(0, 0), GridUtilties.Index2Grid(grid, min));
        Assert.AreEqual(new int2(grid.x - 1, grid.y - 1), GridUtilties.Index2Grid(grid, max));
        
        // Out Of Bounds Checks
        Assert.AreEqual(Invalid_Grid, GridUtilties.Index2Grid(grid, min - 1));
        Assert.AreEqual(Invalid_Grid, GridUtilties.Index2Grid(grid, max + 1));

        // Value Checks
        Assert.AreEqual(new int2(grid.x - 1, 0), GridUtilties.Index2Grid(grid, edge));
        Assert.AreEqual(new int2(0, 1), GridUtilties.Index2Grid(grid, edge + 1));
        Assert.AreEqual(new int2(1, 1), GridUtilties.Index2Grid(grid, edge + 2));
    }

    [Test]
    public void G2I_ValidWithSqureGrids()
    {
        var squareGrid = new int2(512, 512);
        TestIndex(squareGrid);
    }

    [Test]
    public void G2I_ValidWithNonSqureGrids_XGreaterThanY()
    {
        var squareGrid = new int2(512, 256);
        TestIndex(squareGrid);
    }

    [Test]
    public void G2I_ValidWithNonSqureGrids_YGreaterThanX()
    {
        var squareGrid = new int2(256, 512);
        TestIndex(squareGrid);
    }

    [Test]
    public void G2I_ValidWithNonPowerOf2Grids()
    {
        var squareGrid = new int2(7, 7);
        TestIndex(squareGrid);
    }

    static void TestIndex(int2 grid)
    {
        int x_min = 0;
        int x_max = grid.x - 1;

        int y_min = 0;
        int y_max = grid.y - 1;
        
        int min = 0;
        int max = grid.x * grid.y - 1;
        int edge = grid.x - 1;

        // Bounds Checks
        Assert.AreEqual(min, GridUtilties.Grid2Index(grid, new int2(x_min, y_min)));
        Assert.AreEqual(max, GridUtilties.Grid2Index(grid, new int2(x_max, y_max)));
        
        // Out Of Bounds Checks
        Assert.AreEqual(Invalid_Index, GridUtilties.Grid2Index(grid, new int2(x_min - 1, y_min)));
        Assert.AreEqual(Invalid_Index, GridUtilties.Grid2Index(grid, new int2(x_min, y_min - 1)));
        Assert.AreEqual(Invalid_Index, GridUtilties.Grid2Index(grid, new int2(x_max + 1, y_max)));
        Assert.AreEqual(Invalid_Index, GridUtilties.Grid2Index(grid, new int2(x_max, y_max + 1)));

        // Value Checks
        Assert.AreEqual(edge, GridUtilties.Grid2Index(grid, new int2(x_max, y_min)));
        Assert.AreEqual(edge + 1, GridUtilties.Grid2Index(grid, new int2(x_min, y_min + 1)));
        Assert.AreEqual(edge + 2, GridUtilties.Grid2Index(grid, new int2(x_min + 1, y_min + 1)));
    }
}
