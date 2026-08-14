# Backlog

Items planned for future releases. See [changelog.txt](../../changelog.txt) for
what has already shipped.

## UI / Polish

Make the persistence layer co-exist with actual mod settings.

## Vehicle Management

* [x] Assign trucks to the forestry tower instead of each harvester. @Tammy
* [ ] **Truck manager**: Actively manages truck assignments to harvesters to try and optimize wood output (minimize harvester idle time).
* [x] **Planters Trail harvesters**: Replaced by the combined, claim-based Forestry vehicle optimizations. Work-ready planters stage for same-tower active and future harvest opportunities, with global tile exclusivity and replacement when a planter leaves for saplings.
* [x] **Smart harvesters**: Replaced by the combined, claim-based Forestry vehicle optimizations. Idle harvesters stage for the best same-tower future target using maturation and travel time, without an arbitrary horizon.
