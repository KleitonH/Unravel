---
slug: angular-dependency-injection
title: Dependency Injection
order: 7
level: Intermediate
tags: [di, injector, provider, token, inject]
readMinutes: 9
---

## Para que serve

Dependency injection é o padrão arquitetural que o Angular usa para fornecer instâncias de classes (ou valores) a outras classes sem que elas precisem criá-las manualmente. O sistema de DI do Angular resolve dependências em runtime, consultando um grafo hierárquico de injetores. Esse modelo torna o código desacoplado, testável e flexível — uma classe declara o que precisa e o framework cuida de entregar.

## Conceitos: injector, provider, token

Três conceitos formam a base da DI no Angular. O injector é o objeto que mantém o registro de dependências disponíveis em um escopo. O provider é a configuração que diz ao injector como criar ou onde encontrar uma instância. O token é a chave de identificação usada para registrar e recuperar uma dependência — geralmente é a própria classe, mas pode ser um `InjectionToken` para valores que não são classes.

```ts
import { InjectionToken } from '@angular/core';

export interface AppConfig {
  apiUrl: string;
  timeoutMs: number;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('app.config');
```

O `InjectionToken` é usado quando você quer injetar algo que não tem identidade de classe, como uma string de URL ou um objeto de configuração. Sem ele, o TypeScript não consegue gerar metadados de tipo para a injeção.

## Provendo dependências

Existem várias formas de configurar providers. A mais simples é usar `providedIn: 'root'` no decorator `@Injectable`, que registra o service como singleton global. Outras formas envolvem o array `providers` em bootstrap, em uma rota ou no decorator de um componente.

```ts
import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app.component';
import { APP_CONFIG } from './tokens';

bootstrapApplication(AppComponent, {
  providers: [
    { provide: APP_CONFIG, useValue: { apiUrl: '/api', timeoutMs: 5000 } }
  ]
});
```

Os tipos de provider mais comuns são `useClass` (instancia uma classe), `useValue` (fornece um valor fixo), `useFactory` (usa uma função para criar o valor) e `useExisting` (alias para outro token já registrado).

## Injetando dependências

Há duas formas de injetar dependências em uma classe. A clássica usa o construtor com a sintaxe TypeScript de parâmetros tipados. A moderna usa a função `inject()`, disponível desde o Angular 14, que funciona em campos de classe e em funções de inicialização.

```ts
import { Component, inject, Inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { APP_CONFIG, AppConfig } from './tokens';

@Component({
  selector: 'app-cliente',
  standalone: true,
  template: `<p>API: {{ config.apiUrl }}</p>`
})
export class ClienteComponent {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
}

export class OutraForma {
  constructor(
    private http: HttpClient,
    @Inject(APP_CONFIG) private config: AppConfig
  ) {}
}
```

A função `inject()` é preferida em código novo porque elimina o decorator `@Inject` para tokens não-classe e funciona melhor com herança de classes.

## A hierarquia de injetores

Injetores no Angular formam uma árvore que reflete a estrutura de componentes. Quando uma classe pede uma dependência, o Angular procura no injector mais próximo e sobe até encontrar um provider. Se chegar ao topo sem achar, lança um erro `NullInjectorError`.

Isso permite sobrescrever services em escopos específicos. Um componente pode declarar `providers: [LogService]` para receber sua própria instância isolada, enquanto outros componentes continuam usando o singleton global. Esse comportamento é útil para isolamento e para passar configuração diferente em sub-árvores da UI.

```ts
@Component({
  selector: 'app-relatorio',
  standalone: true,
  providers: [
    { provide: APP_CONFIG, useValue: { apiUrl: '/api/v2', timeoutMs: 30000 } }
  ],
  template: `...`
})
export class RelatorioComponent {}
```

Componentes filhos de `RelatorioComponent` que injetarem `APP_CONFIG` recebem a configuração customizada; o resto da aplicação continua com a versão original.

## Modificadores de injeção

Cinco modificadores controlam como o injector busca dependências. `@Optional()` permite que a dependência seja `null` se não houver provider. `@Self()` limita a busca ao injector local. `@SkipSelf()` pula o injector atual e começa pelo pai. `@Host()` para no componente hospedeiro de uma diretiva. `@Inject(TOKEN)` especifica o token quando o tipo TypeScript não é suficiente.

```ts
import { Component, inject } from '@angular/core';
import { LogService } from './log.service';

@Component({ selector: 'app-painel', standalone: true, template: `` })
export class PainelComponent {
  private logOpcional = inject(LogService, { optional: true });
  private logPai = inject(LogService, { skipSelf: true });
}
```

A versão moderna passa os modificadores como segundo argumento de `inject()`. A forma clássica usa decorators no construtor.

## Factory providers

Quando a criação de uma dependência exige lógica, usa-se `useFactory`. A factory é uma função que pode receber outras dependências via o array `deps`. Isso permite construir objetos complexos ou escolher implementações em runtime.

```ts
import { isDevMode } from '@angular/core';

function criarLogger() {
  return isDevMode()
    ? { log: (msg: string) => console.log('[DEV]', msg) }
    : { log: (_: string) => {} };
}

export const loggerProvider = {
  provide: 'LOGGER',
  useFactory: criarLogger
};
```

Factories são comuns em casos como adapters de plataforma, mocks condicionais e configurações lidas do ambiente.

## Provider em rotas e lazy loading

Quando uma rota é carregada com lazy loading, ela cria um injector próprio. Providers declarados nessa rota são visíveis apenas para os componentes carregados por ela e seus descendentes. Esse mecanismo é usado para isolar features e reduzir o tamanho do bundle inicial.

```ts
export const routes = [
  {
    path: 'admin',
    loadComponent: () => import('./admin/admin.component').then(m => m.AdminComponent),
    providers: [AdminService, { provide: APP_CONFIG, useValue: { apiUrl: '/api/admin', timeoutMs: 10000 } }]
  }
];
```

## Armadilhas comuns

Esperar uma instância nova ao injetar um service `providedIn: 'root'` em locais diferentes é um erro — esse provider é singleton. Para múltiplas instâncias, registre o service no `providers` do componente. Outro tropeço é usar `inject()` fora de um contexto de injeção (em callbacks assíncronos, por exemplo), o que lança erro em runtime. Por fim, lembre que `InjectionToken` precisa ser criado com `new InjectionToken<Tipo>('descricao')`; usar apenas uma string como token funciona mas perde a inferência de tipo.
