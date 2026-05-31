---
slug: angular-pipes
title: Pipes Built-in e Customizados
order: 5
level: Beginner
tags: [pipe, transform, formatting, async, pure]
readMinutes: 7
---

## Para que serve

Pipes são funções de transformação aplicadas a valores diretamente no template. Um pipe recebe um valor de entrada, opcionalmente parâmetros, e devolve um valor formatado para exibição. O Angular usa o caractere `|` para aplicar pipes, em uma sintaxe inspirada em shells Unix. Pipes mantêm o template limpo ao mover formatação para fora dos métodos da classe.

## Pipes built-in essenciais

Angular vem com vários pipes prontos para os casos mais comuns de formatação. Os mais usados são `date`, `currency`, `number`, `percent`, `uppercase`, `lowercase`, `titlecase`, `slice`, `json` e `async`. Todos esses pipes estão no `CommonModule`, que precisa ser importado em componentes standalone que os utilizem.

```ts
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-painel-vendas',
  standalone: true,
  imports: [CommonModule],
  template: `
    <h2>{{ titulo | uppercase }}</h2>
    <p>Data: {{ dataPedido | date:'dd/MM/yyyy HH:mm' }}</p>
    <p>Valor: {{ total | currency:'BRL':'symbol':'1.2-2':'pt-BR' }}</p>
    <p>Margem: {{ margem | percent:'1.0-2' }}</p>
    <p>Itens: {{ produtos.length | number }}</p>
    <pre>{{ produtos | json }}</pre>
  `
})
export class PainelVendasComponent {
  titulo = 'Resumo do dia';
  dataPedido = new Date();
  total = 15749.5;
  margem = 0.235;
  produtos = [{ id: 1, nome: 'A' }, { id: 2, nome: 'B' }];
}
```

O pipe `json` é particularmente útil em desenvolvimento para inspecionar objetos diretamente no template. Em produção, ele deve ser removido das views, mas durante debug economiza muito tempo.

## Parâmetros e encadeamento

Pipes aceitam parâmetros separados por dois pontos. O pipe `date`, por exemplo, recebe um formato como primeiro parâmetro: `{{ valor | date:'dd/MM/yyyy' }}`. Múltiplos pipes podem ser encadeados, com a saída de um servindo de entrada para o próximo. A leitura é da esquerda para a direita.

```html
<p>{{ usuario.nome | lowercase | slice:0:10 }}</p>

<p>{{ aniversario | date:'EEEE, dd \'de\' MMMM' | titlecase }}</p>

<p>{{ preco | currency:'USD':'symbol':'1.2-2' }}</p>
```

A ordem importa. `slice:0:10 | uppercase` corta primeiro e depois capitaliza; `uppercase | slice:0:10` capitaliza tudo e depois corta. Resultados podem diferir dependendo dos dados.

## Pipes puros vs impuros

Por padrão, pipes são puros. Um pipe puro só é reavaliado quando o Angular detecta uma mudança de referência ou de valor primitivo na entrada. Isso significa que mutar um array (com `push`, por exemplo) não dispara reavaliação. Para forçar reexecução em cada ciclo de detecção, marca-se o pipe como impuro com `pure: false`.

```ts
import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'filtroAtivos',
  standalone: true,
  pure: false
})
export class FiltroAtivosPipe implements PipeTransform {
  transform<T extends { ativo: boolean }>(itens: T[]): T[] {
    return itens.filter(i => i.ativo);
  }
}
```

Pipes impuros têm custo alto e devem ser evitados. A solução recomendada é manter pipes puros e atualizar dados criando novas referências (`this.itens = [...this.itens, novoItem]` em vez de `this.itens.push(novoItem)`).

## O pipe async

O pipe `async` é uma das peças mais elegantes do Angular. Ele assina automaticamente um `Observable` ou uma `Promise` e desassina quando o componente é destruído, evitando vazamentos de memória. Sempre que o observable emite um novo valor, o template é atualizado.

```ts
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { interval, map } from 'rxjs';

@Component({
  selector: 'app-cronometro',
  standalone: true,
  imports: [CommonModule],
  template: `
    <p>Segundos decorridos: {{ contador$ | async }}</p>
  `
})
export class CronometroComponent {
  contador$ = interval(1000).pipe(map(n => n + 1));
}
```

Sem o pipe `async`, seria necessário assinar manualmente no `ngOnInit`, armazenar a subscription e cancelar no `ngOnDestroy`. O pipe elimina todo esse boilerplate em uma única linha de template.

## Criando um pipe customizado

Um pipe é uma classe decorada com `@Pipe` que implementa a interface `PipeTransform`. O método `transform` recebe o valor e os parâmetros opcionais e retorna o valor transformado. Pipes podem ser standalone, exatamente como componentes e diretivas.

```ts
import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'cpf',
  standalone: true
})
export class CpfPipe implements PipeTransform {
  transform(valor: string | null): string {
    if (!valor) return '';
    const digitos = valor.replace(/\D/g, '').padStart(11, '0');
    return digitos.replace(/(\d{3})(\d{3})(\d{3})(\d{2})/, '$1.$2.$3-$4');
  }
}
```

Para usar: `<span>{{ pessoa.documento | cpf }}</span>`. O pipe é puro por padrão, então só será reexecutado quando o valor de `pessoa.documento` mudar de referência ou primitivo.

## Localização e o pipe date

O pipe `date` usa o locale registrado da aplicação para formatar nomes de meses, dias e separadores. Para apps em português, é necessário registrar o locale `pt-BR` no `main.ts` ou em um provider. Sem isso, datas são formatadas em inglês.

```ts
import { registerLocaleData } from '@angular/common';
import localePt from '@angular/common/locales/pt';
import { LOCALE_ID } from '@angular/core';

registerLocaleData(localePt);

bootstrapApplication(AppComponent, {
  providers: [{ provide: LOCALE_ID, useValue: 'pt-BR' }]
});
```

## Armadilhas comuns

Esquecer de importar o `CommonModule` em componentes standalone faz pipes built-in deixarem de funcionar silenciosamente — o Angular trata como expressão desconhecida e renderiza vazio. Outro erro comum é usar pipes impuros desnecessariamente, prejudicando performance. Por fim, ao criar pipes customizados que retornam objetos, lembre que o pipe puro só reavalia quando a referência muda; pipes que filtram listas devem ser cuidadosamente projetados para evitar resultados obsoletos.
