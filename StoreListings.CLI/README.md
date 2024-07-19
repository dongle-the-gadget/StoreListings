# StoreListings
A wrapper for the Microsoft Store products API (for package querying) and Windows Update (for getting package download links)

## Project structure
### StoreListings.Library
The main library, responsible for contacting the Microsoft Store APIs. These include:
- StoreEdgeFD (`https://storeedgefd.dsx.mp.microsoft.com/v9.0`): to query package information and download links for unpackaged apps. Functionality in `StoreEdgeFDProduct.cs`.
- DCAT (`https://displaycatalog.mp.microsoft.com/v7.0`): to query package dependency information. Functionality in `DCATPackage.cs`.
- FE3 (`https://fe3cr.delivery.mp.microsoft.com`): to get package download links. Functionality in `FE3Handler.cs`.

### StoreListings.CLI
A CLI program to test the functionality of `StoreListings.Library`. 
The program allows querying package information and retrieving download links from Microsoft Store product IDs. 
It uses [`ConsoleAppFramework`](https://github.com/Cysharp/ConsoleAppFramework) for Native AOT compatibility.

## Features
- Native AOT compatible.
- Unpackaged applications support.
- Grouping of download links and dependencies by app versions.
- Filtering of incompatible packages.
- *Does not show BlockMap files.*

## Limitations
- Package grouping breaks if DCAT and FE3 disagree.
  + Real-world example: Python 3.12
- No Windows 8 support. (Do they still serve those files?)
- No MSIXVC support.
- Only Product ID is supported currently, PFN is feasible but not yet done.

## TODOs
- Investigate Windows 8 support. (If the Store APIs still serve Windows 8)
- Add PFN querying.
- Make a website to use `StoreListings`?

## Credits
Special thanks to [Gustave Monce](https://github.com/gus33000) and [Joshua "Yoshi" Askharoun](https://github.com/yoshiask) for providing resources on the functions of the Store APIs.

## License
[MIT License](LICENSE.txt)
